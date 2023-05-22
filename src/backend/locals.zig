const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const errors = @import("../errors.zig");
const Token = @import("../frontend/lexer.zig").Token;

const BindingKind = @import("../frontend/nodes/ast.zig").BindingKind;
const CompilerError = errors.CompilerError;

pub const Local = struct {
    // Use of token to report error location
    identifier: Token,
    kind: BindingKind,
    initialised: bool,
    depth: u8,
    index: u8,

    const Self = @This();

    pub fn create() Self {
        return .{
            .name = undefined,
            .kind = BindingKind.None,
            .initialised = false,
            .depth = 0,
            .index = 0,
        };
    }

    pub inline fn isGlobal(self: *Self) bool {
        return self.depth == 0;
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
            .index = 0,
        };
    }
};

pub const LocalTable = struct {
    locals: ArrayList(Local),
    depth: u8,

    const Self = @This();

    pub fn init(allocator: Allocator) Self {
        return .{
            .locals = ArrayList(Local).init(allocator),
            .depth = 0,
        };
    }

    pub fn deinit(self: *Self) void {
        self.locals.deinit();
    }

    pub fn add(self: *Self, local: Local) !void {
        for (self.locals.items) |*item| {
            if (item.depth < self.depth) break;

            if (local.depth == item.depth and identifiersEqual(&local.identifier, &item.identifier)) {
                errors.errorWithToken(&local.identifier, "Compiler", "Local is already defined");
                return CompilerError.LocalDefined;
            }
        }

        try self.locals.append(local);
    }

    pub fn find(self: *Self, identifier: *Token) ?*Local {
        for (self.locals.items) |*item| {
            if (identifiersEqual(identifier, &item.identifier)) {
                return item;
            }
        }
        return null;
    }

    pub fn findErr(self: *Self, identifier: *Token) !*Local {
        if (self.find(identifier)) |local| {
            return local;
        }
        return CompilerError.UndefinedLocal;
    }

    pub fn lastLocal(self: *Self) !u8 {
        if (self.locals.items.len >= std.math.maxInt(u8)) {
            return CompilerError.TooManyLocals;
        }
        return @intCast(u8, self.locals.items.len);
    }

    pub inline fn has(self: *Self, identifier: *Token) bool {
        return self.find(identifier) != null;
    }

    inline fn identifiersEqual(a: *const Token, b: *const Token) bool {
        return std.mem.eql(u8, a.lexeme, b.lexeme);
    }
};
