const std = @import("std");
const Allocator = std.mem.Allocator;

const debug = @import("../debug.zig");
const ByteCode = @import("bytecode.zig").ByteCode;
const Chunk = @import("chunk.zig").Chunk;
const Value = @import("../runtime/value.zig").Value;
const Object = @import("../runtime/object.zig").Object;
const VM = @import("../runtime/vm.zig").VM;
const errors = @import("../errors.zig");
const CompilerError = errors.CompilerError;

const ast = @import("../frontend/nodes/ast.zig");
const Ast = ast.AstNode;
const Token = @import("../frontend/lexer.zig").Token;

const scopeCompiler = @import("scopeCompiler.zig");
const ScopeCompiler = scopeCompiler.ScopeCompiler;
const UpValue = scopeCompiler.Upvalue;
const ScopeKind = scopeCompiler.ScopeKind;
const locals = @import("locals.zig");
const Local = locals.Local;
const BindingKind = ast.BindingKind;

pub const Compiler = struct {
    scopeComp: *ScopeCompiler,
    vm: *VM,
    canAssign: bool,

    const Self = @This();

    pub fn init(vm: *VM) Self {
        return .{
            .scopeComp = undefined,
            .vm = vm,
            .canAssign = false,
        };
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

    pub inline fn beginScope(self: *Self) CompilerError!void {
        if (self.scopeComp.depth == std.math.maxInt(u8)) {
            return CompilerError.TooManyScopes;
        }
        self.scopeComp.depth += 1;
    }

    pub inline fn endScope(self: *Self) !void {
        self.scopeComp.depth -= 1;

        var currentLocals = &self.scopeComp.locals;
        while (currentLocals.items.len > 0 and
            currentLocals.items[currentLocals.items.len - 1].depth > self.scopeComp.depth)
        {
            if (currentLocals.items[currentLocals.items.len - 1].captured) {
                try self.emitOp(.CloseUpvalue);
            } else {
                try self.emitOp(.Pop);
            }
            _ = currentLocals.pop();
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
    // FIXME: Make a Table that caches these
    inline fn identifierConstant(self: *Self, token: *Token) !u8 {
        return try self.makeConstant(token, Value.fromObject(&(try Object.String.copy(self.vm, token.lexeme)).object));
    }

    fn declareVariable(self: *Self, identifier: *Token, kind: BindingKind) !void {
        if (self.scopeComp.depth == 0) return;

        for (0..self.scopeComp.locals.items.len) |idx| {
            const local = self.scopeComp.locals.items[self.scopeComp.locals.items.len - 1 - idx];
            if (ScopeCompiler.identifiersEqual(identifier, &local.identifier)) {
                errors.errorWithToken(identifier, "Compiler", "Already a variable defined with this name");
                return CompilerError.LocalDefined;
            }
        }

        try self.scopeComp.addLocal(identifier, .{
            .identifier = identifier.*,
            .kind = kind,
            .initialised = false,
            .captured = false,
            .depth = self.scopeComp.depth,
        });
    }

    fn initialiseVariable(self: *Self, identifier: *Token) !void {
        if (self.scopeComp.depth == 0) return;
        if (self.scopeComp.getLocal(identifier)) |local| {
            local.initialised = true;
        }
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
            return CompilerError.TooManyConstants;
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
            .identifier => |n| try self.identifierExpr(n),
            .assignment => |n| try self.assignment(n),
            .functionCall => |n| try self.functionCall(n),

            .literal => |n| try self.literal(n),
            .binary => |n| try self.binary(n),
            .body => |n| try self.body(n),
            .ret => |n| try self.returnStatement(n),

            .out => |n| try self.out(n),

            .expressionStmt => |n| {
                try self.visit(n.inner);
                try self.emitOp(.Pop);
            },

            else => std.debug.panic("UNIMPLEMENTED: '{s}'\n", .{@tagName(node)}),
        }
    }

    fn letBinding(self: *Self, node: *ast.LetBinding) !void {
        if (node.rhs.isFunctionDef()) {
            try self.functionDef(&node.token, node.rhs.asFunctionDef());
        } else {
            try self.visit(node.rhs);
        }

        try self.declareVariable(&node.token, node.kind);
        try self.initialiseVariable(&node.token);

        if (self.scopeComp.depth == 0) {
            try self.emitOpByte(.SetGlobal, try self.identifierConstant(&node.token));
        } else {
            try self.emitOpByte(.SetLocal, @intCast(u8, self.scopeComp.localCount() - 1));
        }
    }

    fn functionDef(self: *Self, identifier: *Token, node: *ast.FunctionDef) !void {
        var scopeComp = try self.newScopeCompiler(.Function);
        defer scopeComp.deinit();

        try self.setCompiler(&scopeComp, identifier.lexeme);
        try self.beginScope();

        // VISIT PARAMETER list
        if (node.parameters) |params| {
            _ = params;
        }

        if (node.body.isBlock()) {
            try self.body(node.body.asBlock());
        } else {
            // Single expression function - wrap with return
            try self.visit(node.body);
            try self.emitOp(.Return);
        }

        try self.endScope();
        const function = try self.endCompiler();

        try self.emitOpByte(.Closure, try self.makeConstant(
            identifier,
            Value.fromObject(&function.object),
        ));

        for (scopeComp.upvalues.items) |*upvalue| {
            try self.emitBytes(
                if (upvalue.isLocal) 1 else 0,
                upvalue.index,
            );
        }
    }

    fn resolveUpvalue(self: *Self, comp: *ScopeCompiler, identifier: *Token) !?u8 {
        if (comp.enclosing) |enclosing| {
            if (enclosing.findLocal(identifier)) |local| {
                enclosing.locals.items[@intCast(usize, local)].captured = true;
                return try comp.addUpvalue(identifier, local, true);
            }

            if (try self.resolveUpvalue(enclosing, identifier)) |upvalue| {
                return try comp.addUpvalue(identifier, upvalue, false);
            }
        }
        return null;
    }

    fn identifierExpr(self: *Self, node: *ast.Identifier) !void {
        var setOp: ByteCode = undefined;
        var getOp: ByteCode = undefined;
        var index: u8 = undefined;

        const maybeLocal = self.scopeComp.findLocal(&node.token);

        if (self.scopeComp.depth > 0 and maybeLocal != null) {
            setOp = .SetLocal;
            getOp = .GetLocal;
            index = maybeLocal.?;
        } else if (try self.resolveUpvalue(self.scopeComp, &node.token)) |upvalue| {
            setOp = .SetUpvalue;
            getOp = .GetUpvalue;
            index = upvalue;
        } else if (self.scopeComp.depth == 0) {
            setOp = .SetGlobal;
            getOp = .GetGlobal;
            index = try self.identifierConstant(&node.token);
        }

        if (self.canAssign) {
            try self.emitOpByte(setOp, index);
        } else {
            try self.emitOpByte(getOp, index);
        }
    }

    fn assignment(self: *Self, node: *ast.Assignment) !void {
        self.canAssign = true;
        try self.visit(node.rhs);
        try self.visit(node.lhs);
        self.canAssign = false;

        // TODO: Handle different types of assignment
    }

    fn functionCall(self: *Self, node: *ast.FunctionCall) !void {
        for (node.arguments.items) |arg| {
            try self.visit(arg);
        }

        if (node.arguments.items.len == std.math.maxInt(u8)) {
            errors.errorWithToken(&node.token, "Compiler", "Too many arguments in call to out");
            return CompilerError.TooManyArguments;
        }

        try self.emitOpByte(.Call, @intCast(
            u8,
            node.arguments.items.len,
        ));
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

    fn returnStatement(self: *Self, node: *ast.Return) !void {
        if (self.scopeComp.depth == 0) {
            errors.errorWithToken(&node.token, "Compiler", "Cannot return from global scope");
            return CompilerError.ReturnFromGlobal;
        }

        if (node.expression) |value| {
            try self.visit(value);
        } else {
            try self.emitOp(.Nil);
        }
        try self.emitOp(.Return);
    }

    fn out(self: *Self, node: *ast.Out) !void {
        for (node.arguments.items) |arg| {
            try self.visit(arg);
        }

        if (node.arguments.items.len == std.math.maxInt(u8)) {
            errors.errorWithToken(&node.token, "Compiler", "Too many arguments in call to out");
            return CompilerError.TooManyArguments;
        }

        try self.emitOpByte(.Out, @intCast(
            u8,
            node.arguments.items.len,
        ));
    }
};
