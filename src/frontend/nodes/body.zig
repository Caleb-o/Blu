const std = @import("std");
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;

const Ast = @import("ast.zig").AstNode;

pub const BodyNode = struct {
    enclosing: ?*BodyNode,
    inner: ArrayList(Ast),

    const Self = @This();

    pub fn init(allocator: Allocator, enclosing: ?*BodyNode) !*Self {
        const node = try allocator.create(Self);
        node.* = .{
            .enclosing = enclosing,
            .inner = ArrayList(Ast).init(allocator),
        };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        for (self.inner.items) |*node| {
            node.deinit(allocator);
        }

        self.inner.deinit();
        allocator.destroy(self);
    }
};
