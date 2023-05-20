const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const Token = @import("../lexer.zig").Token;
const ast = @import("ast.zig");
const Ast = ast.AstNode;
const Body = ast.Body;

pub const FunctionDefinitionNode = struct {
    identifier: Token,
    parameters: ?Ast,
    body: Ast,

    const Self = @This();

    pub fn init(
        allocator: Allocator,
        identifier: Token,
        parameters: ?Ast,
        body: Ast,
    ) !*Self {
        const node = try allocator.create(Self);
        node.* = .{
            .identifier = identifier,
            .parameters = parameters,
            .body = body,
        };
        return node;
    }

    pub fn deinit(self: *Self, allocator: Allocator) void {
        if (self.parameters) |*params| {
            params.deinit(allocator);
        }
        self.body.deinit(allocator);
        allocator.destroy(self);
    }
};
