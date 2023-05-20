const std = @import("std");
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;

pub const IdentifierNode = struct {
    token: Token,

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        token: Token,
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{ .token = token };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        allocator.destroy(self);
    }
};
