const std = @import("std");
const ArrayList = std.ArrayList;
const Object = @import("../runtime/object.zig").Object;

const errors = @import("../errors.zig");

const Token = @import("../frontend/lexer.zig").Token;
const VM = @import("../runtime/vm.zig").VM;
const CompilerError = errors.CompilerError;
const Local = @import("locals.zig").Local;

// Functions and lambdas are treated differently
// Functions cannot view locals outside its scope,
// only globals, whereas a lambda will capture.
pub const ScopeKind = enum { Script, Function, Lambda };

pub const Upvalue = struct {
    index: u8,
    isLocal: bool,
};

pub const ScopeCompiler = struct {
    enclosing: ?*ScopeCompiler,
    kind: ScopeKind,
    function: *Object.Function,
    depth: u8,
    locals: ArrayList(Local),
    upvalues: ArrayList(Upvalue),

    const Self = @This();

    pub fn init(vm: *VM, depth: u8, kind: ScopeKind, enclosing: ?*ScopeCompiler) !Self {
        return .{
            .enclosing = enclosing,
            .kind = kind,
            .function = try Object.Function.create(vm),
            .depth = depth,
            .locals = ArrayList(Local).init(vm.allocator),
            .upvalues = ArrayList(Upvalue).init(vm.allocator),
        };
    }

    pub fn deinit(self: *Self) void {
        self.upvalues.deinit();
        self.locals.deinit();
    }

    pub inline fn localCount(self: *Self) usize {
        return self.locals.items.len;
    }

    pub inline fn upvalueCount(self: *Self) usize {
        return self.upvalues.items.len;
    }

    pub fn findLocal(self: *Self, token: *Token) ?u8 {
        for (0..self.locals.items.len) |idx| {
            const local = self.locals.items[self.locals.items.len - 1 - idx];
            if (identifiersEqual(token, &local.identifier)) {
                return local.index;
            }
        }
        return null;
    }

    pub fn getLocal(self: *Self, token: *Token) ?*Local {
        for (0..self.locals.items.len) |idx| {
            const local = &self.locals.items[self.locals.items.len - 1 - idx];
            if (identifiersEqual(token, &local.identifier)) {
                return local;
            }
        }
        return null;
    }

    pub fn addLocal(self: *Self, token: *Token, local: Local) !void {
        if (self.locals.items.len == std.math.maxInt(u8)) {
            errors.errorWithToken(token, "Compiler", "Too many locals in scope");
            return CompilerError.TooManyLocals;
        }

        for (self.locals.items) |loc| {
            if (loc.depth == self.depth and identifiersEqual(token, &loc.identifier)) {
                errors.errorWithToken(token, "Compiler", "Local already defined");
                return CompilerError.LocalDefined;
            }
        }

        try self.locals.append(local);
    }

    pub fn addUpvalue(self: *Self, token: *Token, index: u8, isLocal: bool) !u8 {
        // Try to find existing upvalue
        for (self.upvalues.items, 0..) |*upvalue, idx| {
            if (upvalue.index == index and upvalue.isLocal == isLocal) {
                return @intCast(u8, idx);
            }
        }

        if (self.upvalueCount() == std.math.maxInt(u8)) {
            errors.errorWithToken(token, "Compiler", "Too many upvalues");
            return CompilerError.TooManyUpvalues;
        }

        try self.upvalues.append(.{
            .index = index,
            .isLocal = isLocal,
        });

        self.function.upvalues += 1;
        return @intCast(u8, self.upvalues.items.len - 1);
    }

    pub inline fn identifiersEqual(a: *const Token, b: *const Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
    }
};
