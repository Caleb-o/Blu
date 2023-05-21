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

        try self.defineNatives();

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
            std.debug.print("\n{d}.\n", .{self.scopeComp.depth});
            func.chunk.disassemble(func.getIdentifier());
        }

        self.closeCompiler();
        return func;
    }

    // ====== NATIVES
    fn printNative(vm: *VM, args: []Value) Value {
        _ = vm;
        args[0].print();
        return Value.fromNil();
    }

    fn defineNatives(self: *Self) !void {
        try self.defineNative("print", 1, printNative);
    }

    fn defineNative(self: *Self, name: []const u8, arity: u8, func: Object.ZigFunc) !void {
        try self.vm.push(Value.fromObject(&(try Object.String.fromLiteral(self.vm, name)).object));
        try self.vm.push(Value.fromObject(&(try Object.NativeFunction.create(self.vm, name, arity, func)).object));

        var id = self.vm.peek(1);
        var f = self.vm.peek(0);

        _ = self.vm.globals.set(id.asObject().asString(), f);

        _ = try self.vm.pop();
        _ = try self.vm.pop();

        try self.addLocalAssume(name, .None);
        self.markInitialised();
    }

    // ====== LOCALS
    // FIXME: Make a Table that caches these
    inline fn identifierConstant(self: *Self, token: *Token) !u8 {
        return try self.makeConstant(token, Value.fromObject(&(try Object.String.copy(self.vm, token.lexeme)).object));
    }

    inline fn identifiersEqual(a: *Token, b: *Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
    }

    fn addUpvalue(self: *Self, comp: *ScopeCompiler, token: *Token, index: u8, isLocal: bool) !u8 {
        _ = self;
        // Don't add another upvalue, if it already exists
        for (0..comp.function.upvalueCount) |idx| {
            const upvalue = &comp.upvalues.items[idx];
            if (upvalue.index == index and upvalue.isLocal == isLocal) {
                return @intCast(u8, idx);
            }
        }

        // Check if upvalue count is max
        const current = comp.function.upvalueCount;
        if (current == std.math.maxInt(u8)) {
            errors.errorWithToken(token, "Compiler", "Too many closure variables in function");
            return CompilerError.TooManyUpvalues;
        }

        try comp.upvalues.append(.{
            // NOTE: Needs to be run-time index, not global index
            .index = index,
            .isLocal = isLocal,
        });
        comp.function.upvalueCount += 1;
        return current;
    }

    fn resolveUpvalue(self: *Self, comp: *ScopeCompiler, name: *Token) !?u8 {
        if (comp.enclosing == null) return null;

        if (self.resolveLocal(comp.enclosing.?, name)) |local| {
            return try self.addUpvalue(comp, name, local, true);
        }

        if (try self.resolveUpvalue(comp.enclosing.?, name)) |upvalue| {
            return try self.addUpvalue(comp, name, upvalue, false);
        }

        return null;
    }

    fn resolveLocal(self: *Self, comp: *ScopeCompiler, name: *Token) ?u8 {
        _ = self;
        for (comp.locals.items, 0..) |*local, idx| {
            if (identifiersEqual(name, &local.identifier)) {
                if (!local.initialised) {
                    errors.errorWithToken(name, "Compiler", "Cannot read uninitialised local variable");
                    return null;
                }

                return @intCast(u8, idx);
            }
        }

        return null;
    }

    fn addLocalAssume(self: *Self, name: []const u8, kind: ast.BindingKind) !void {
        try self.scopeComp.locals.append(.{
            .identifier = Token.aritificial(name),
            .kind = kind,
            .initialised = false,
            .isCaptured = false,
            .depth = @intCast(u8, self.scopeComp.depth),
            .index = @intCast(u8, self.scopeComp.locals.items.len),
        });
    }

    fn addLocal(self: *Self, name: *Token, kind: ast.BindingKind) !void {
        if (self.scopeComp.locals.items.len == std.math.maxInt(u8)) {
            errors.errorWithToken(name, "Compiler", "Too many variables in function.");
            return;
        }

        try self.scopeComp.locals.append(.{
            .identifier = name.*,
            .kind = kind,
            .initialised = false,
            .depth = @intCast(u8, self.scopeComp.depth),
            .index = @intCast(u8, self.scopeComp.locals.items.len),
            .isCaptured = false,
        });
    }

    fn declareVariable(self: *Self, name: *Token, kind: ast.BindingKind) !void {
        if (self.resolveLocal(self.scopeComp, name)) |_| {
            errors.errorWithToken(name, "Compiler", "There is already a local with that name");
            return CompilerError.LocalDefined;
        }

        try self.addLocal(name, kind);
    }

    fn markInitialised(self: *Self) void {
        std.debug.assert(self.scopeComp.locals.items.len > 0);
        const count = self.scopeComp.locals.items.len - 1;
        const local = &self.scopeComp.locals.items[count];
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

            .literal => |n| try self.literal(n),
            .binary => |n| try self.binary(n),
            .body => |n| try self.body(n),

            .identifier => |n| try self.identifier(n),
            .assignment => |n| try self.assignment(n),

            .parameterList => |n| try self.parameterList(n),
            .functionCall => |n| try self.functionCall(n),
            .expressionStmt => |n| try self.expressionStatement(n),

            // Should be handled by let
            .functionDef => unreachable,
            else => std.debug.panic("UNIMPLEMENTED: '{s}'\n", .{@tagName(node)}),
        }
    }

    fn letBinding(self: *Self, node: *ast.LetBinding) !void {
        if (node.rhs.isFunctionDef()) {
            return try self.functionDef(
                node.rhs.asFunctionDef(),
                node.kind,
            );
        }

        try self.visit(node.rhs);

        // Global
        const local = self.resolveLocal(self.scopeComp, &node.token);
        if (local != null) {
            errors.errorWithToken(&node.token, "Compiler", "Binding already defined");
            return CompilerError.SymbolDefined;
        }

        // Create and initialise the variable
        try self.declareVariable(&node.token, node.kind);
        self.markInitialised();

        if (self.scopeComp.depth == 0) {
            // Global
            const location = try self.identifierConstant(&node.token);
            try self.emitOpByte(.SetGlobal, location);
        } else {
            // Local
            try self.emitOpByte(.SetLocal, @intCast(u8, self.scopeComp.locals.items.len - 1));
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

    fn identifier(self: *Self, node: *ast.Identifier) !void {
        var getOp: ByteCode = undefined;
        var setOp: ByteCode = undefined;
        var arg: u8 = undefined;
        var resolved = self.resolveLocal(self.scopeComp, &node.token);

        if (resolved) |res| {
            arg = res;
            getOp = .GetLocal;
            setOp = .SetLocal;
        } else {
            const isUpvalue = try self.resolveUpvalue(self.scopeComp, &node.token);
            if (isUpvalue) |upvalue| {
                arg = upvalue;
                getOp = .GetUpvalue;
                setOp = .SetUpvalue;
            } else {
                arg = try self.identifierConstant(&node.token);
                getOp = .GetGlobal;
                setOp = .SetGlobal;
            }
        }

        if (self.canAssign) {
            try self.emitOpByte(setOp, arg);
        } else {
            try self.emitOpByte(getOp, arg);
        }
    }

    fn assignment(self: *Self, node: *ast.Assignment) !void {
        self.canAssign = true;
        defer self.canAssign = false;

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
    }

    fn parameterList(self: *Self, node: *ast.ParameterList) !void {
        if (node.list.items.len >= std.math.maxInt(u8)) {
            errors.errorWithToken(&node.token, "Compiler", "Function has too many parameters");
            return CompilerError.TooManyArguments;
        }

        const arity = @intCast(u8, node.list.items.len);
        self.scopeComp.function.arity = arity;

        for (node.list.items) |*param| {
            const p = param.asIdentifier();
            try self.declareVariable(&p.token, ast.BindingKind.None);
            self.markInitialised();
        }
    }

    fn functionDef(self: *Self, node: *ast.FunctionDef, kind: ast.BindingKind) !void {
        var comp = try self.newScopeCompiler(.Function);
        try self.setCompiler(&comp, node.identifier.lexeme);

        if (node.parameters) |*params| {
            try self.parameterList(params.asParameterList());
        }

        if (node.body.isBlock()) {
            var block = node.body.asBlock();
            for (block.inner.items) |n| {
                try self.visit(n);
            }
        } else {
            // Implicit return
            try self.visit(node.body);
            try self.emitOp(.Return);
        }

        const func = try self.endCompiler();
        try self.emitOpByte(
            .Closure,
            try self.makeConstant(&node.identifier, Value.fromObject(&func.object)),
        );

        // OP_CLOSURE is a special variable-sized instruction
        for (0..@intCast(usize, func.upvalueCount)) |idx| {
            const upvalue = &self.scopeComp.upvalues.items[idx];
            try self.emitBytes(
                if (upvalue.isLocal) 1 else 0,
                upvalue.index,
            );
        }

        try self.declareVariable(&node.identifier, kind);
        self.markInitialised();

        // FIXME: Allow .SetUpvalue
        if (self.scopeComp.depth == 0) {
            const idLoc = try self.identifierConstant(&node.identifier);
            try self.emitOpByte(.SetGlobal, idLoc);
        } else {
            try self.emitOpByte(.SetLocal, @intCast(u8, self.scopeComp.locals.items.len - 1));
        }
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
