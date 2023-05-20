const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const root = @import("root");
const Token = root.lexer.Token;

pub const Local = struct {
    // Use of token to report error location
    identifier: Token,
    initialised: bool,
    depth: i32,

    const Self = @This();

    pub fn create() Self {
        return .{
            .name = undefined,
            .kind = .Local,
            .initialised = false,
            .depth = -1,
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
            .kind = .Local,
            .depth = 0,
        };
    }
};

pub const LocalTable = struct {
    locals: ArrayList(Local),
    currentDepth: u32,

    const Self = @This();

    pub fn init(allocator: Allocator) Self {
        return .{
            .locals = ArrayList(Local).init(allocator),
            .currentDepth = 0,
        };
    }

    pub fn deinit(self: *Self) void {
        self.locals.deinit();
    }

    pub inline fn append(self: *Self, local: Local) !void {
        try self.locals.append(local);
    }
};
