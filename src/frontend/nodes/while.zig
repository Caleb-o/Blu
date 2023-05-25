const std = @import("std");
const Allocator = std.mem.Allocator;

const Ast = @import("ast.zig").AstNode;

pub const WhileNode = struct {
    condition: Ast,
    body: Ast,

    const Self = @This();

    pub fn init(allocator: Allocator, condition: Ast, body: Ast) !*Self {
        const node = try allocator.create(Self);
        node.* = .{
            .condition = condition,
            .body = body,
        };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        self.condition.deinit(allocator);
        self.body.deinit(allocator);
        allocator.destroy(self);
    }
};
