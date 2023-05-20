const std = @import("std");
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const ReturnNode = struct {
    token: Token,
    expression: ?Ast,

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        token: Token,
        expression: ?Ast,
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{ .token = token, .expression = expression };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        if (self.expression) |*expr| {
            expr.deinit(allocator);
        }
        allocator.destroy(self);
    }
};
