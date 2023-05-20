const std = @import("std");
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const ExpressionStmtNode = struct {
    inner: Ast,

    const Self = @This();

    pub fn init(allocator: Allocator, inner: Ast) !*Self {
        const node = try allocator.create(Self);
        node.* = .{ .inner = inner };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        self.inner.deinit(allocator);
        allocator.destroy(self);
    }
};
