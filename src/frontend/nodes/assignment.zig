const std = @import("std");
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const AssignmentNode = struct {
    operator: Token,
    lhs: Ast,
    rhs: Ast,

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        operator: Token,
        lhs: Ast,
        rhs: Ast,
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{
            .operator = operator,
            .lhs = lhs,
            .rhs = rhs,
        };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        self.lhs.deinit(allocator);
        self.rhs.deinit(allocator);
        allocator.destroy(self);
    }
};
