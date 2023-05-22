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
const UpValue = scopeCompiler.Upvalue;
const ScopeKind = scopeCompiler.ScopeKind;
const locals = @import("locals.zig");
const Local = locals.Local;
const LocalTable = locals.LocalTable;
const BindingKind = root.ast.BindingKind;

pub const Compiler = struct {
    scopeComp: *ScopeCompiler,
    vm: *VM,
    locals: LocalTable,
    canAssign: bool,

    const Self = @This();

    pub fn init(vm: *VM) Self {
        return .{
            .scopeComp = undefined,
            .vm = vm,
            .locals = LocalTable.init(vm.allocator),
            .canAssign = false,
        };
    }

    pub fn deinit(self: *Self) void {
        self.locals.deinit();
    }

    pub fn run(self: *Self, rootNode: Ast) !*Object.Function {
        var scopeComp = try ScopeCompiler.init(self.vm, 0, .Script, null);
        try self.setCompiler(&scopeComp, null);

        try self.visit(rootNode);
        return self.endCompiler();
    }

    // ====== UTILITIES
    inline fn newScopeCompiler(self: *Self, kind: ScopeKind) !ScopeCompiler {
        return ScopeCompiler.init(self.vm, self.locals.depth + 1, kind, self.scopeComp);
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
        if (self.locals.depth == std.math.maxInt(u8)) {
            return CompilerError.TooManyScopes;
        }
        self.locals.depth += 1;
    }

    pub inline fn endScope(self: *Self) void {
        self.locals.depth -= 1;
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
        try self.locals.add(.{
            .identifier = identifier.*,
            .kind = kind,
            .initialised = false,
            .depth = self.locals.depth,
            .index = self.scopeComp.locals,
        });
        self.scopeComp.locals += 1;
    }

    fn initialiseVariable(self: *Self, identifier: *Token) !void {
        const local = try self.locals.findErr(identifier);
        local.initialised = true;
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
            .identifier => |n| try self.identifierExpr(n),
            .assignment => |n| try self.assignment(n),

            .literal => |n| try self.literal(n),
            .binary => |n| try self.binary(n),
            .body => |n| try self.body(n),

            .expressionStmt => |n| try self.visit(n.inner),

            else => std.debug.panic("UNIMPLEMENTED: '{s}'\n", .{@tagName(node)}),
        }
    }

    fn letBinding(self: *Self, node: *ast.LetBinding) !void {
        try self.declareVariable(&node.token, node.kind);

        if (node.rhs.isFunctionDef()) {
            try self.functionDef(&node.token, node.rhs.asFunctionDef());
        } else {
            try self.visit(node.rhs);
        }

        try self.initialiseVariable(&node.token);

        if (self.locals.depth == 0) {
            try self.emitOpByte(.SetGlobal, try self.identifierConstant(&node.token));
        } else {
            try self.emitOpByte(.SetLocal, self.scopeComp.locals - 1);
        }
    }

    fn functionDef(self: *Self, identifier: *Token, node: *ast.FunctionDef) !void {
        var scopeComp = try self.newScopeCompiler(.Function);
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

        self.endScope();
        const func = try self.endCompiler();

        try self.emitOpByte(.Closure, try self.makeConstant(
            identifier,
            Value.fromObject(&func.object),
        ));
    }

    fn identifierExpr(self: *Self, node: *ast.Identifier) !void {
        const maybeLocal = self.locals.find(&node.token);
        if (maybeLocal == null) {
            errors.errorWithToken(&node.token, "Compiler", "Undefined local");
            return CompilerError.UndefinedLocal;
        }

        const local = maybeLocal.?;
        var setOp: ByteCode = undefined;
        var getOp: ByteCode = undefined;
        var index: u8 = local.index;

        if (local.depth == 0) {
            setOp = .SetGlobal;
            getOp = .GetGlobal;
            index = try self.identifierConstant(&node.token);
        } else if (local.depth >= self.scopeComp.depth) {
            setOp = .SetLocal;
            getOp = .GetLocal;
        } else {
            // TODO: Upvalue
            std.debug.panic("UPVALUE\n", .{});
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
};
