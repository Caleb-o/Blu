const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const ParameterListNode = struct {
    token: Token,
    list: ArrayList(Ast),

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        token: Token,
        list: ArrayList(Ast),
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{ .token = token, .list = list };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        for (self.list.items) |*node| {
            node.deinit(allocator);
        }
        self.list.deinit();
        allocator.destroy(self);
    }
};
