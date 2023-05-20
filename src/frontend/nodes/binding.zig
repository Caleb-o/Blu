const std = @import("std");
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const Ast = @import("ast.zig").AstNode;

pub const BindingKind = enum {
    None,
    Mutable,
    Recursive,
};

pub const BindingNode = struct {
    token: Token,
    kind: BindingKind,
    final: bool,
    rhs: Ast,

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        token: Token,
        kind: BindingKind,
        final: bool,
        rhs: Ast,
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{
            .token = token,
            .kind = kind,
            .final = final,
            .rhs = rhs,
        };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        self.rhs.deinit(allocator);
        allocator.destroy(self);
    }
};
