const std = @import("std");
const Allocator = std.mem.Allocator;

const root = @import("root");
const debug = root.debug;
const ByteCode = @import("bytecode.zig").ByteCode;
const Value = root.value.Value;
const Chunk = root.chunk.Chunk;
const Object = root.object.Object;
const VM = root.vm.VM;
const errors = root.errors;
const CompilerError = errors.CompilerError;

const ast = root.ast;
const Ast = ast.AstNode;
const Token = root.lexer.Token;

const scopeCompiler = @import("scopeCompiler.zig");
const ScopeCompiler = scopeCompiler.ScopeCompiler;
const ScopeKind = scopeCompiler.ScopeKind;
const locals = @import("locals.zig");
const Local = locals.Local;
const LocalTable = locals.LocalTable;

pub const Compiler = struct {
    scopeComp: *ScopeCompiler,
    localTable: LocalTable,
    vm: *VM,

    const Self = @This();

    pub fn init(allocator: Allocator, vm: *VM) Self {
        return .{
            .scopeComp = undefined,
            .localTable = LocalTable.init(allocator),
            .vm = vm,
        };
    }

    pub fn deinit(self: *Self) void {
        self.localTable.deinit();
    }

    pub fn run(self: *Self, rootNode: Ast) !*Object.Function {
        var scopeComp = try ScopeCompiler.init(self.vm, 0, .Script, null);
        try self.setCompiler(&scopeComp, null);

        try self.visit(rootNode);
        return self.endCompiler();
    }

    // ====== UTILITIES
    inline fn newScopeCompiler(self: *Self, kind: ScopeKind) !ScopeCompiler {
        return ScopeCompiler.init(self.vm, self.scopeComp.depth + 1, kind, self.scopeComp);
    }

    fn setCompiler(self: *Self, compiler: *ScopeCompiler, id: ?[]const u8) !void {
        self.scopeComp = compiler;

        if (self.scopeComp.kind != .Script) {
            // Sanity check
            std.debug.assert(id != null);
            self.scopeComp.function.identifier =
                try Object.String.copy(self.vm, id.?);
        }
    }

    inline fn closeCompiler(self: *Self) void {
        if (self.scopeComp.enclosing) |enclosing| {
            self.scopeComp = enclosing;
        }
    }

    pub inline fn beginScope(self: *Self) void {
        self.scopeComp.depth += 1;
    }

    pub fn endScope(self: *Self) !void {
        self.scopeComp.depth -= 1;

        const localCount = self.localTable.locals.items.len;
        const compCount = self.scopeComp.locals;

        var count: u8 = 0;
        for (0..compCount) |_| {
            const local = &self.localTable.locals.items[localCount - 1];
            if (local.depth <= self.scopeComp.depth) break;

            if (count == std.math.maxInt(u8)) {
                return CompilerError.TooManyLocals;
            }
            count += 1;
        }

        if (count == 1) {
            try self.emitOp(.Pop);
        } else if (count > 1) {
            try self.emitOpByte(.PopN, count);
        }
    }

    /// Emit return and close the current compiler
    fn endCompiler(self: *Self) !*Object.Function {
        try self.emitReturn();
        const func = self.scopeComp.function;

        if (debug.PRINT_CODE) {
            func.chunk.disassemble(func.getIdentifier());
        }

        self.closeCompiler();
        return func;
    }

    // ====== LOCALS
    inline fn identifierConstant(self: *Self, token: *Token) !u8 {
        return try self.makeConstant(token, Value.fromObject(&(try Object.String.copy(self.vm, token.lexeme)).object));
    }

    inline fn identifiersEqual(a: *Token, b: *Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
    }

    fn lastLocal(self: *Self) ?Local {
        if (self.localTable.locals.items.len == 0) return null;
        const count = self.localTable.locals.items.len - 1;
        return self.localTable.locals.items[count];
    }

    fn resolveLocal(self: *Self, name: *Token) i32 {
        if (self.scopeComp.locals == 0) return -1;
        const count: usize = self.localTable.locals.items.len;

        var i: usize = 0;
        while (i < self.scopeComp.locals) : (i += 1) {
            var local = self.localTable.locals.items[count - 1 - i];
            if (identifiersEqual(name, &local.identifier)) {
                if (!local.initialised) {
                    errors.errorWithToken(name, "Compiler", "Cannot read local variable in its own initialiser.");
                    return -1;
                }

                return @intCast(i32, i);
            }
        }
        return -1;
    }

    fn addLocal(self: *Self, name: *Token) !void {
        if (self.scopeComp.locals == std.math.maxInt(u8)) {
            errors.errorWithToken(name, "Compiler", "Too many variables in function.");
            return;
        }

        try self.localTable.locals.append(.{
            .identifier = name.*,
            .initialised = false,
            .depth = -1,
        });
        self.scopeComp.locals += 1;
    }

    inline fn markScope(self: *Self) void {
        const idx = self.localTable.locals.items.len - 1;
        self.localTable.locals.items[idx].depth = self.scopeComp.depth;
    }

    inline fn markInitialised(self: *Self) void {
        const idx = self.localTable.locals.items.len - 1;
        self.localTable.locals.items[idx].initialised = true;
    }

    fn declareGlobal(self: *Self, token: *Token) !void {
        const globalIdx = try self.identifierConstant(token);
        try self.emitOpByte(.GetGlobal, globalIdx);

        const string = try Object.String.fromLiteral(self.vm, token.lexeme);
        try self.vm.push(Value.fromObject(&string.object));
        _ = self.vm.globals.set(string, Value.fromNil());
    }

    fn declareVariable(self: *Self, name: *Token) !void {
        const count = self.localTable.locals.items.len;

        var i: usize = 0;
        while (i < count) : (i += 1) {
            const local = &self.localTable.locals.items[count - 1 - i];
            if (local.depth != -1 and local.depth < self.scopeComp.depth) {
                break;
            }

            if (identifiersEqual(name, &local.identifier)) {
                errors.errorWithToken(name, "Compiler", "Already a variable with this name in this scope.");
            }
        }

        try self.addLocal(name);
    }

    // ====== CHUNK & EMITTERS
    inline fn currentChunk(self: *Self) *Chunk {
        return &self.scopeComp.function.chunk;
    }

    inline fn emitOp(self: *Self, op: ByteCode) !void {
        try self.currentChunk().write(@intCast(u8, @enumToInt(op)));
    }

    inline fn emitOps(self: *Self, op1: ByteCode, op2: ByteCode) !void {
        try self.currentChunk().write(@intCast(u8, @enumToInt(op1)));
        try self.currentChunk().write(@intCast(u8, @enumToInt(op2)));
    }

    inline fn emitOpByte(self: *Self, op1: ByteCode, op2: u8) !void {
        try self.currentChunk().write(@intCast(u8, @enumToInt(op1)));
        try self.currentChunk().write(op2);
    }

    inline fn emitByte(self: *Self, op: u8) !void {
        try self.currentChunk().write(op);
    }

    inline fn emitBytes(self: *Self, op1: u8, op2: u8) !void {
        try self.currentChunk().write(op1);
        try self.currentChunk().write(op2);
    }

    inline fn emitReturn(self: *Self) !void {
        try self.emitOps(.Nil, .Return);
    }

    fn makeConstant(self: *Self, token: *Token, value: Value) !u8 {
        var constant = self.currentChunk().addConstant(value) catch {
            errors.errorWithToken(token, "Compiler", "Too many constants in one chunk.");
            return 0;
        };

        return @intCast(u8, constant);
    }

    fn emitConstant(self: *Self, token: *Token, value: Value) !void {
        const location = try self.makeConstant(token, value);
        try self.emitOpByte(.ConstantByte, location);
    }

    // ====== VISITORS

    fn visit(self: *Self, node: Ast) CompilerError!void {
        switch (node) {
            .binding => |n| try self.letBinding(n),

            .literal => |n| try self.literal(n),
            .binary => |n| try self.binary(n),
            .body => |n| try self.body(n),

            .identifier => |n| try self.identifier(n),
            .assignment => |n| try self.assignment(n),

            .parameterList => |n| try self.parameterList(n),
            .functionDef => |n| try self.functionDef(n),
            .functionCall => |n| try self.functionCall(n),
            .expressionStmt => |n| try self.expressionStatement(n),
            else => {
                std.debug.print("UNIMPLEMENTED: '{s}'\n", .{@tagName(node)});
                unreachable;
            },
        }
    }

    fn letBinding(self: *Self, node: *ast.LetBinding) !void {
        const exists = self.resolveLocal(&node.token);
        _ = exists;
    }

    fn literal(self: *Self, node: *ast.Literal) !void {
        const value = switch (node.token.kind) {
            .Number => Value.fromF32(try std.fmt.parseFloat(f32, node.token.lexeme)),
            else => unreachable,
        };

        try self.emitConstant(&node.token, value);
    }

    fn binary(self: *Self, node: *ast.Binary) !void {
        try self.visit(node.lhs);
        try self.visit(node.rhs);

        try switch (node.token.kind) {
            .Plus => self.emitOp(.Add),
            .Minus => self.emitOp(.Sub),
            .Star => self.emitOp(.Mul),
            .Slash => self.emitOp(.Div),

            .StarStar => self.emitOp(.Pow),
            .Percent => self.emitOp(.Mod),

            else => {},
        };
    }

    fn body(self: *Self, node: *ast.Body) !void {
        for (node.inner.items) |n| {
            try self.visit(n);
        }
    }

    fn identifier(self: *Self, node: *ast.Identifier) !void {
        const index = self.resolveLocal(&node.token);
        if (index == -1) {
            errors.errorWithToken(&node.token, "Compiler", "Undefined local");
            return CompilerError.UndefinedLocal;
        }

        try self.emitOpByte(.GetLocal, @intCast(u8, index));
    }

    fn assignment(self: *Self, node: *ast.Assignment) !void {
        try self.visit(node.lhs);
        try self.visit(node.rhs);

        switch (node.operator.kind) {
            // Operation before
            .PlusEqual => {
                try self.visit(node.lhs);
                try self.emitOp(.Add);
            },
            .Equal => {},
            else => std.debug.panic("UNIMPLEMENTED ASSIGN : {}\n", .{node.operator.kind}),
        }

        // const local = switch (node.lhs) {
        //     .identifier => self.lastLocal(),
        //     else => std.debug.panic("UNIMPLEMENTED ASSIGN LHS : {}\n", .{node.operator.kind}),
        // };

        // std.debug.assert(local != null);

        // if (local) |item| {
        //     self.markInitialised();

        //     if (item.kind == .Global) {
        //         const globalIdx = try self.identifierConstant(&node.lhs.asIdentifier().token);
        //         try self.emitOpByte(.SetGlobal, globalIdx);
        //     } else {
        //         const index = @intCast(u8, self.localTable.locals.items.len - 1);
        //         try self.emitOpByte(.SetLocal, @intCast(u8, index));
        //     }
        // }
    }

    fn parameterList(self: *Self, node: *ast.ParameterList) !void {
        if (node.list.items.len >= std.math.maxInt(u8)) {
            errors.errorWithToken(&node.token, "Compiler", "Function has too many parameters");
        } else {
            const arity = @intCast(u8, node.list.items.len);
            self.scopeComp.function.arity = arity;
        }

        for (node.list.items) |*param| {
            const p = param.asIdentifier();
            try self.declareVariable(&p.token);
        }
    }

    fn functionDef(self: *Self, node: *ast.FunctionDef) !void {
        var comp = try self.newScopeCompiler(.Function);
        try self.setCompiler(&comp, node.identifier.lexeme);
        self.beginScope();

        if (node.parameters) |params| {
            try self.visit(params);
        }
        try self.visit(node.body);

        try self.endScope();
        const func = try self.endCompiler();
        try self.emitConstant(&node.identifier, Value.fromObject(&func.object));

        const idLoc = try self.identifierConstant(&node.identifier);
        try self.emitOpByte(.SetLocal, idLoc);

        try self.declareVariable(&node.identifier);
        self.markScope();
        self.markInitialised();
    }

    fn functionCall(self: *Self, node: *ast.FunctionCall) !void {
        for (node.arguments.items) |item| {
            try self.visit(item);
        }
        const count = node.arguments.items.len;
        if (count >= std.math.maxInt(u8)) {
            errors.errorWithToken(&node.token, "Compiler", "Too many arguments in call");
            return CompilerError.TooManyArguments;
        }
        try self.emitOpByte(.Call, @intCast(u8, count));
    }

    fn expressionStatement(self: *Self, node: *ast.ExpressionStmt) !void {
        try self.visit(node.inner);

        // NOTE: Since Ruby has no declaration of variables
        //       and it only uses assignment, we can't always pop in
        //       an expression statement.
        // TODO: Make this also depend on if the variable has been declared prior, then
        //       a pop is allowed.
        if (node.inner != .assignment) {
            try self.emitOp(.Pop);
        }
    }
};
