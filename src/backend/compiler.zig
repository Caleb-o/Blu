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

    pub inline fn beginScope(self: *Self) void {
        self.scopeComp.depth += 1;
    }

    pub inline fn endScope(self: *Self) void {
        self.scopeComp.depth -= 1;
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

    inline fn identifiersEqual(a: *Token, b: *Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
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
            .literal => |n| try self.literal(n),
            .binary => |n| try self.binary(n),
            .body => |n| try self.body(n),

            else => std.debug.panic("UNIMPLEMENTED: '{s}'\n", .{@tagName(node)}),
        }
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
