const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const OutNode = struct {
    token: Token,
    arguments: ArrayList(Ast),

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        token: Token,
        arguments: ArrayList(Ast),
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{ .token = token, .arguments = arguments };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        for (self.arguments.items) |*node| {
            node.deinit(allocator);
        }
        allocator.destroy(self);
    }
};
