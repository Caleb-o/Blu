const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;
const Arena = std.heap.ArenaAllocator;
const GPA = std.heap.GeneralPurposeAllocator(.{});

const errors = @import("root").errors;
const ParserError = errors.ParserError;

const ast = @import("nodes/ast.zig");
const Ast = ast.AstNode;

const scanner = @import("lexer.zig");
const Lexer = scanner.Lexer;
const Token = scanner.Token;
const TokenKind = scanner.TokenKind;

const prec = @import("precedence.zig");
const Precedence = prec.Precedence;

pub const Parser = struct {
    lexer: Lexer,
    previous: Token,
    current: Token,
    // The parser will deallocate all the nodes itself
    allocator: Allocator,

    root: *ast.Body,
    currentBlock: *ast.Body,
    hadError: bool,

    const Self = @This();

    pub fn init(allocator: Allocator, source: []const u8) Self {
        var lexer = Lexer.init(source);
        const token = lexer.getToken();
        return .{
            .lexer = lexer,
            .previous = undefined,
            .current = token,
            .allocator = allocator,
            .root = undefined,
            .currentBlock = undefined,
            .hadError = false,
        };
    }

    pub fn deinit(self: *Self) void {
        self.root.deinit(self.allocator);
    }

    pub fn parse(self: *Self) !Ast {
        self.root = try ast.Body.init(self.allocator, null);
        self.currentBlock = self.root;

        while (!self.match(.Eof)) {
            self.append(try self.declaration());
        }

        return Ast.fromBlock(self.root);
    }

    // ====== Utility

    fn setNextBlock(self: *Self, body: *ast.Body) void {
        var oldBlock = self.currentBlock;
        self.currentBlock = body;
        body.enclosing = oldBlock;
    }

    fn popBlock(self: *Self) void {
        std.debug.assert(self.currentBlock.enclosing != null);
        self.currentBlock = self.currentBlock.enclosing.?;
    }

    inline fn append(self: *Self, node: Ast) void {
        self.currentBlock.inner.append(node) catch unreachable;
    }

    /// Advance regardless of token
    inline fn advance(self: *Self) void {
        self.previous = self.current;
        self.current = self.lexer.getToken();
    }

    /// Check if token matches, no consume
    inline fn check(self: *Self, kind: TokenKind) bool {
        if (self.current.kind != kind) return false;
        return true;
    }

    /// Check if token matches any kind, with no consume
    fn checkAny(self: *Self, kind: []const TokenKind) bool {
        for (kind) |k| {
            if (self.current.kind == k) {
                return true;
            }
        }
        return false;
    }

    /// Check if token matches, then consume if true
    inline fn match(self: *Self, kind: TokenKind) bool {
        if (self.current.kind != kind) return false;
        self.advance();
        return true;
    }

    /// Check if token matches any kind, then consume if true
    fn matchAny(self: *Self, kind: []const TokenKind) bool {
        for (kind) |k| {
            if (self.current.kind == k) {
                self.advance();
                return true;
            }
        }
        return false;
    }

    /// Consume token if it matches, otherwise error
    // TODO: Allow for sync to progress
    fn consume(self: *Self, kind: TokenKind, msg: []const u8) ParserError!void {
        if (self.current.kind == kind) {
            self.advance();
            return;
        }

        errors.errorWithToken(&self.current, "Parser", msg);
        return ParserError.MalformedCode;
    }

    pub fn parsePrecendence(self: *Self, precedence: Precedence) !Ast {
        self.advance();
        var node = try self.prefix(self.previous.kind);

        while (@enumToInt(precedence) <= @enumToInt(prec.getPrecedence(self.current.kind))) {
            self.advance();
            const op = self.previous;
            const rhs = try self.infix(self.previous.kind);
            node = Ast.fromBinary(try ast.Binary.init(self.allocator, op, node, rhs));
        }
        return node;
    }

    pub fn prefix(self: *Self, kind: TokenKind) ParserError!Ast {
        return switch (kind) {
            // .LeftParen => self.groupedExpression(),
            .Bang => try self.unary(.Not),
            .Plus => try self.unary(.Unary),
            .Minus => try self.unary(.UnaryMinus),

            .Nil, .True, .False, .String, .Number => try self.primary(),

            // .Identifier => try self.variable(.Local),

            // Should error instead
            else => std.debug.panic("Unimplemented prefix '{}'\n", .{kind}),
        };
    }

    pub fn infix(self: *Self, kind: TokenKind) ParserError!Ast {
        return switch (kind) {
            .Plus, .Minus, .Star, .Slash, .StarStar, .Percent => try self.binary(),
            // .LeftParen => try self.call(),

            else => std.debug.panic("Unimplemented infix '{}'\n", .{kind}),
        };
    }

    // ====== CODE PARSING
    fn declaration(self: *Self) !Ast {
        if (self.match(.Let)) {
            const let = try self.letDeclaration();
            try self.consume(.Semicolon, "Expect ';' after let declaration");
            return let;
        }
        return try self.statement();
    }

    fn letDeclaration(self: *Self) !Ast {
        const is_final = self.match(.Final);
        const kind = switch (self.current.kind) {
            .Mutable => ast.BindingKind.Mutable,
            .Recursive => ast.BindingKind.Recursive,
            else => ast.BindingKind.None,
        };

        if (kind != ast.BindingKind.None) {
            _ = self.advance();
        }

        const id = self.current;
        try self.consume(.Identifier, "Expect identifier in let binding");

        if (self.checkAny(&[_]TokenKind{ .Identifier, .LeftParen })) {
            // Function definition
            return try self.letFunctionDefinition(id, kind, is_final);
        } else {
            try self.consume(.Equal, "Expect '=' after let binding");

            return Ast.fromLetBinding(try ast.LetBinding.init(
                self.allocator,
                id,
                kind,
                is_final,
                try self.expression(),
            ));
        }
    }

    fn letFunctionDefinition(self: *Self, id: Token, kind: ast.BindingKind, isFinal: bool) !Ast {
        const parameters = try self.parameterList();
        try self.consume(.Equal, "Expect '=' after parameter list");

        const body = Ast.fromFunctionDef(try ast.FunctionDef.init(
            self.allocator,
            id,
            parameters,
            if (self.match(.LeftCurly)) try self.block() else try self.expression(),
        ));

        return Ast.fromLetBinding(try ast.LetBinding.init(
            self.allocator,
            id,
            kind,
            isFinal,
            body,
        ));
    }

    fn identifier(self: *Self, msg: []const u8) !Ast {
        const id = self.current;
        try self.consume(.Identifier, msg);
        return Ast.fromIdentifier(try ast.Identifier.init(self.allocator, id));
    }

    fn parameterList(self: *Self) !Ast {
        const token = self.previous;
        var list = ArrayList(Ast).init(self.allocator);

        if (self.match(.LeftParen)) {
            // No parameters
            try self.consume(.RightParen, "Expect ')' after '(' in function parameter list");
        } else if (!self.check(.Equal)) {
            while (self.match(.Identifier)) {
                try list.append(try self.identifier("Expect identifier in parameter list"));
            }
        }

        return Ast.fromParameterList(try ast.ParameterList.init(
            self.allocator,
            token,
            list,
        ));
    }

    fn block(self: *Self) ParserError!Ast {
        var newBlock = try ast.Body.init(self.allocator, self.currentBlock);
        self.setNextBlock(newBlock);

        while (!self.match(.Eof) and !self.match(.RightCurly)) {
            self.append(try self.declaration());
        }

        self.popBlock();
        return Ast.fromBlock(newBlock);
    }

    fn returnStatement(self: *Self) !Ast {
        const token = self.current;
        self.advance();
        const expr = if (!self.check(.Semicolon)) try self.expression() else null;
        return Ast.fromReturn(try ast.Return.init(
            self.allocator,
            token,
            expr,
        ));
    }

    fn statement(self: *Self) !Ast {
        const stmt = switch (self.current.kind) {
            .Return => try self.returnStatement(),
            else => try self.expressionStatement(),
        };
        try self.consume(.Semicolon, "Expect ';' after statement");
        return stmt;
    }

    fn expressionStatement(self: *Self) !Ast {
        const expr = try self.expression();
        const exprstmt = try ast.ExpressionStmt.init(self.allocator, expr);
        return Ast.fromExpressionStmt(exprstmt);
    }

    inline fn expression(self: *Self) !Ast {
        return try self.parsePrecendence(.Assignment);
    }

    fn binary(self: *Self) !Ast {
        const operator = self.previous;
        const lhs = try self.parsePrecendence(prec.getPrecedence(operator.kind).next());

        // This will just make sure that we have the correct op
        switch (operator.kind) {
            .Plus, .Minus => {},
            .Star, .Slash, .Percent => {},
            .StarStar => {},
            else => unreachable,
        }
        return lhs;
    }

    pub fn unary(self: *Self, precedence: Precedence) !Ast {
        const operator = self.previous;
        switch (operator.kind) {
            .Bang, .Minus => {},
            else => unreachable,
        }
        return try self.parsePrecendence(precedence);
    }

    pub fn argumentList(self: *Self) !ArrayList(Ast) {
        var list = ArrayList(Ast).init(self.allocator);
        if (!self.match(.RightParen)) {
            try list.append(try self.expression());
            while (self.match(.Comma)) {
                try list.append(try self.expression());
            }
            try self.consume(.RightParen, "Expect ')' after argument list");
        }

        return list;
    }

    pub fn call(self: *Self) !Ast {
        const lhs = self.previous;
        const arguments = try self.argumentList();
        return Ast.fromFunctionCall(try ast.FunctionCall.init(
            self.allocator,
            lhs,
            arguments,
        ));
    }

    fn pow(self: *Self) !Ast {
        const operator = self.previous;
        switch (operator.kind) {
            .StarStar => {},
            else => unreachable,
        }
        return try self.parsePrecendence(prec.getPrecedence(operator.kind).next());
    }

    fn primary(self: *Self) !Ast {
        switch (self.previous.kind) {
            .True, .False, .Nil, .String, .Number => {
                return Ast.fromLiteral(try ast.Literal.init(self.allocator, self.previous));
            },
            else => {
                errors.errorWithToken(&self.current, "Parser", "Unknown symbol found in expression");
                return ParserError.UnknownSymbol;
            },
        }
    }
};
