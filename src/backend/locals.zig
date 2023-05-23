const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const errors = @import("../errors.zig");
const Token = @import("../frontend/lexer.zig").Token;

const BindingKind = @import("../frontend/nodes/ast.zig").BindingKind;
const CompilerError = errors.CompilerError;
const ScopeCompiler = @import("scopeCompiler.zig").ScopeCompiler;

pub const Local = struct {
    // Use of token to report error location
    identifier: Token,
    kind: BindingKind,
    initialised: bool,
    captured: bool,
    depth: u8,

    const Self = @This();

    pub fn create() Self {
        return .{
            .name = undefined,
            .kind = BindingKind.None,
            .initialised = false,
            .captured = false,
            .depth = 0,
        };
    }

    // Some locals require an artificial token, as a token does not exist
    // in the context of the call
    pub fn artificial(identifier: []const u8) Self {
        return .{
            .name = Token{
                .kind = .String,
                .lexeme = identifier,
                .column = 1,
                .line = 1,
            },
            .kind = BindingKind.None,
            .initialised = false,
            .depth = 0,
        };
    }
};

pub const LocalTable = struct {
    data: ArrayList(Local),
    depth: u8,

    const Self = @This();

    pub fn init(allocator: Allocator) Self {
        return .{
            .data = ArrayList(Local).init(allocator),
            .depth = 0,
        };
    }

    pub fn deinit(self: *Self) void {
        self.data.deinit();
    }

    pub fn findLocal(self: *Self, token: *Token) ?u8 {
        for (0..self.data.items.len) |idx| {
            const local = self.data.items[self.data.items.len - 1 - idx];
            if (identifiersEqual(token, &local.identifier)) {
                return @intCast(u8, self.data.items.len - 1 - idx);
            }
        }
        return null;
    }

    pub fn getLocal(self: *Self, token: *Token) ?*Local {
        for (0..self.data.items.len) |idx| {
            const local = &self.data.items[self.data.items.len - 1 - idx];
            if (identifiersEqual(token, &local.identifier)) {
                return local;
            }
        }
        return null;
    }

    pub fn addLocal(self: *Self, token: *Token, local: Local) !void {
        if (self.data.items.len == std.math.maxInt(u8)) {
            errors.errorWithToken(token, "Compiler", "Too many locals in scope");
            return CompilerError.TooManyLocals;
        }

        for (self.data.items) |loc| {
            if (loc.depth == self.depth and identifiersEqual(token, &loc.identifier)) {
                errors.errorWithToken(token, "Compiler", "Local already defined");
                return CompilerError.LocalDefined;
            }
        }

        try self.data.append(local);
    }

    pub inline fn identifiersEqual(a: *const Token, b: *const Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
    }
};
