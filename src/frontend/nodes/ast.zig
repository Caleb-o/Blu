const std = @import("std");
const Allocator = std.mem.Allocator;

// Expose to Ast viewers
pub const LetBindingNode = @import("binding.zig");
pub const LetBinding = LetBindingNode.BindingNode;
pub const BindingKind = LetBindingNode.BindingKind;
pub const Literal = @import("literal.zig").LiteralNode;
pub const Binary = @import("binary.zig").BinaryNode;
pub const Body = @import("body.zig").BodyNode;
pub const identifier = @import("identifier.zig");
pub const Identifier = identifier.IdentifierNode;
pub const Assignment = @import("assignment.zig").AssignmentNode;
pub const ParameterList = @import("parameterList.zig").ParameterListNode;
pub const FunctionCall = @import("functionCall.zig").FunctionCallNode;
pub const Out = @import("out.zig").OutNode;
pub const FunctionDef = @import("functionDefinition.zig").FunctionDefinitionNode;
pub const Return = @import("return.zig").ReturnNode;
pub const While = @import("while.zig").WhileNode;
pub const ExpressionStmt = @import("expressionStmt.zig").ExpressionStmtNode;

pub const AstNode = union(enum) {
    binding: *LetBinding,
    literal: *Literal,
    binary: *Binary,
    body: *Body,
    identifier: *Identifier,
    assignment: *Assignment,
    parameterList: *ParameterList,
    functionDef: *FunctionDef,
    functionCall: *FunctionCall,
    out: *Out,
    ret: *Return,
    whileStmt: *While,
    expressionStmt: *ExpressionStmt,

    const Self = @This();

    // Utils
    pub inline fn fromLetBinding(node: *LetBinding) Self {
        return .{ .binding = node };
    }

    pub inline fn fromLiteral(node: *Literal) Self {
        return .{ .literal = node };
    }

    pub inline fn fromBinary(node: *Binary) Self {
        return .{ .binary = node };
    }

    pub inline fn fromBlock(node: *Body) Self {
        return .{ .body = node };
    }

    pub inline fn fromIdentifier(node: *Identifier) Self {
        return .{ .identifier = node };
    }

    pub inline fn fromAssignment(node: *Assignment) Self {
        return .{ .assignment = node };
    }

    pub inline fn fromParameterList(node: *ParameterList) Self {
        return .{ .parameterList = node };
    }

    pub inline fn fromFunctionCall(node: *FunctionCall) Self {
        return .{ .functionCall = node };
    }

    pub inline fn fromOut(node: *Out) Self {
        return .{ .out = node };
    }

    pub inline fn fromWhile(node: *While) Self {
        return .{ .whileStmt = node };
    }

    pub inline fn fromFunctionDef(node: *FunctionDef) Self {
        return .{ .functionDef = node };
    }

    pub inline fn fromReturn(node: *Return) Self {
        return .{ .ret = node };
    }

    pub inline fn fromExpressionStmt(node: *ExpressionStmt) Self {
        return .{ .expressionStmt = node };
    }

    // IS
    pub inline fn isLetBinding(self: *Self) bool {
        return self.* == .binding;
    }

    pub inline fn isLiteral(self: *Self) bool {
        return self.* == .literal;
    }

    pub inline fn isBinary(self: *Self) bool {
        return self.* == .binary;
    }

    pub inline fn isBlock(self: *Self) bool {
        return self.* == .body;
    }

    pub inline fn isIdentifier(self: *Self) bool {
        return self.* == .identifier;
    }

    pub inline fn isAssignment(self: *Self) bool {
        return self.* == .assignment;
    }

    pub inline fn isParameterList(self: *Self) bool {
        return self.* == .parameterList;
    }

    pub inline fn isFunctionCall(self: *Self) bool {
        return self.* == .functionCall;
    }

    pub inline fn isOut(self: *Self) bool {
        return self.* == .out;
    }

    pub inline fn isWhile(self: *Self) bool {
        return self.* == .whileStmt;
    }

    pub inline fn isFunctionDef(self: *Self) bool {
        return self.* == .functionDef;
    }

    pub inline fn isReturn(self: *Self) bool {
        return self.* == .ret;
    }

    pub inline fn isExpressionStmt(self: *Self) bool {
        return self.* == .expressionStmt;
    }

    // AS -- Asserts are here for my sake of sanity
    pub fn asLetBinding(self: *Self) *LetBinding {
        std.debug.assert(self.isLetBinding());
        return self.binding;
    }

    pub fn asLiteral(self: *Self) *Literal {
        std.debug.assert(self.isLiteral());
        return self.literal;
    }

    pub fn asBinary(self: *Self) *Binary {
        std.debug.assert(self.isBinary());
        return self.binary;
    }

    pub fn asBlock(self: *Self) *Body {
        std.debug.assert(self.isBlock());
        return self.body;
    }

    pub fn asIdentifier(self: *Self) *Identifier {
        std.debug.assert(self.isIdentifier());
        return self.identifier;
    }

    pub fn asAssignment(self: *Self) *Assignment {
        std.debug.assert(self.isAssignment());
        return self.assignment;
    }

    pub fn asParameterList(self: *Self) *ParameterList {
        std.debug.assert(self.isParameterList());
        return self.parameterList;
    }

    pub fn asFunctionCall(self: *Self) *FunctionCall {
        std.debug.assert(self.isFunctionCall());
        return self.functionCall;
    }

    pub fn asOut(self: *Self) *Out {
        std.debug.assert(self.isOut());
        return self.out;
    }

    pub fn asFunctionDef(self: *Self) *FunctionDef {
        std.debug.assert(self.isFunctionDef());
        return self.functionDef;
    }

    pub fn asWhile(self: *Self) *While {
        std.debug.assert(self.isWhile());
        return self.whileStmt;
    }

    pub fn asReturn(self: *Self) *Return {
        std.debug.assert(self.isReturn());
        return self.ret;
    }

    pub fn asExpressionStmt(self: *Self) *ExpressionStmt {
        std.debug.assert(self.isExpressionStmt());
        return self.expressionStmt;
    }

    pub inline fn deinit(self: *Self, allocator: Allocator) void {
        switch (self.*) {
            .binding => self.asLetBinding().deinit(allocator),
            .literal => self.asLiteral().deinit(allocator),
            .binary => self.asBinary().deinit(allocator),
            .body => self.asBlock().deinit(allocator),
            .identifier => self.asIdentifier().deinit(allocator),
            .assignment => self.asAssignment().deinit(allocator),
            .parameterList => self.asParameterList().deinit(allocator),
            .functionCall => self.asFunctionCall().deinit(allocator),
            .out => self.asOut().deinit(allocator),
            .functionDef => self.asFunctionDef().deinit(allocator),
            .ret => self.asReturn().deinit(allocator),
            .whileStmt => self.asWhile().deinit(allocator),
            .expressionStmt => self.asExpressionStmt().deinit(allocator),
        }
    }

    pub inline fn print(self: *Self) void {
        self.printLisp();
    }

    fn printLisp(self: *Self) void {
        switch (self.*) {
            .literal => |v| {
                std.debug.print("{s}", .{v.token.lexeme});
            },
            .binary => |v| {
                std.debug.print("({s} ", .{v.token.lexeme});
                v.lhs.printLisp();
                std.debug.print(" ", .{});
                v.rhs.printLisp();
                std.debug.print(")", .{});
            },
            .body => |v| {
                std.debug.print("(", .{});
                for (v.inner.items, 0..) |*i, idx| {
                    i.printLisp();
                    if (idx < v.inner.items.len - 1) {
                        std.debug.print(" ", .{});
                    }
                }
                std.debug.print(")", .{});
            },
            .identifier => |v| {
                switch (v.kind) {
                    .Local => std.debug.print("Local", .{}),
                    .Global => std.debug.print("Global", .{}),
                    .Instance => std.debug.print("Instance", .{}),
                }
                std.debug.print(" '{s}'", .{v.token.lexeme});
            },
            .assignment => |v| {
                std.debug.print("({s} ", .{v.operator.lexeme});
                v.lhs.printLisp();
                std.debug.print(" ", .{});
                v.rhs.printLisp();
                std.debug.print(")", .{});
            },
            .parameterList => |v| {
                std.debug.print("(", .{});
                for (v.list.items, 0..) |*item, idx| {
                    item.print();
                    if (idx < v.list.items.len - 1) {
                        std.debug.print(" ", .{});
                    }
                }
                std.debug.print(")", .{});
            },
            .functionCall => |v| {
                std.debug.print("(", .{});
                for (v.arguments.items, 0..) |*item, idx| {
                    item.print();
                    if (idx < v.list.items.len - 1) {
                        std.debug.print(" ", .{});
                    }
                }
                std.debug.print(")", .{});
            },
            .out => |v| {
                std.debug.print("(out ", .{});
                for (v.arguments.items, 0..) |*item, idx| {
                    item.print();
                    if (idx < v.list.items.len - 1) {
                        std.debug.print(" ", .{});
                    }
                }
                std.debug.print(")", .{});
            },
            .functionDef => |v| {
                std.debug.print("({s} ", .{v.identifier.lexeme});
                if (v.parameters) |*params| {
                    params.print();
                }
                v.body.print();
                std.debug.print(")", .{});
            },
            .ret => |v| {
                std.debug.print("(return", .{});
                if (v.expression) |*expr| {
                    std.debug.print(" ", .{});
                    expr.print();
                }
                std.debug.print(")", .{});
            },
            .whileStmt => |v| {
                std.debug.print("(while ", .{});
                v.condition.print();
                std.debug.print(" ", .{});
                v.body.print();
                std.debug.print(")", .{});
            },
            .expressionStmt => |v| {
                std.debug.print("(", .{});
                v.inner.print();
                std.debug.print(")", .{});
            },
        }
    }
};
