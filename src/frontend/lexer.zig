const std = @import("std");
const mem = std.mem;

pub const TokenKind = enum {
    // Single-character tokens.
    LeftParen,
    RightParen,
    LeftSquare,
    RightSquare,
    LeftCurly,
    RightCurly,

    Comma,
    Dot,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Semicolon,
    Colon,

    // One, two or three character tokens.
    PlusEqual,
    MinusEqual,
    StarEqual,
    SlashEqual,
    PercentEqual,
    StarStar,

    DotDot,
    DotDotEqual,

    Bang,
    BangEqual,
    Equal,
    EqualEqual,
    EqualEqualEqual,
    Greater,
    GreaterGreater,
    GreaterEqual,
    Less,
    LessLess,
    LessEqual,

    // Literals.
    Identifier,
    String,
    Number,

    // Keywords.
    And,
    Or,
    Break,
    Case,
    Object,
    Else,
    Export,
    Final,
    False,
    For,
    True,
    If,
    In,
    Let,
    Mutable,
    Module,
    Nil,
    Not,
    Return,
    Recursive,
    Then,
    While,

    Artificial,
    Error,
    Eof,
};

pub const Token = struct {
    kind: TokenKind,
    line: u32,
    column: u32,
    lexeme: []const u8,

    pub fn aritificial(lexeme: []const u8) Token {
        return .{
            .kind = .Artificial,
            .line = 1,
            .column = 1,
            .lexeme = lexeme,
        };
    }
};

pub const Lexer = struct {
    start: []const u8,
    current: usize,
    line: u32,

    const Self = @This();

    pub fn init(source: []const u8) Self {
        return .{
            .start = source,
            .current = 0,
            .line = 1,
        };
    }

    pub fn getToken(self: *Self) Token {
        self.skipWhitespace();
        self.setStart();

        if (self.isAtEnd()) {
            return self.makeToken(.Eof);
        }

        var c = self.advance();

        if (isAlpha(c)) return self.identifier();
        if (isDigit(c)) return self.number();

        return switch (c) {
            '(' => self.makeToken(.LeftParen),
            ')' => self.makeToken(.RightParen),
            '[' => self.makeToken(.LeftSquare),
            ']' => self.makeToken(.RightSquare),
            '{' => self.makeToken(.LeftCurly),
            '}' => self.makeToken(.RightCurly),
            ',' => self.makeToken(.Comma),
            '.' => self.makeToken(.Dot),

            '+' => self.makeToken(if (self.match('=')) .PlusEqual else .Plus),
            '-' => self.makeToken(if (self.match('=')) .MinusEqual else .Minus),
            '*' => if (self.match('*'))
                self.makeToken(.StarStar)
            else
                self.makeToken(.Star),
            '/' => self.makeToken(if (self.match('=')) .SlashEqual else .Slash),
            '%' => self.makeToken(if (self.match('=')) .PercentEqual else .Percent),

            ';' => self.makeToken(.Semicolon),

            '!' => self.makeToken(if (self.match('=')) .BangEqual else .Bang),
            '=' => self.makeToken(if (self.match('=')) .EqualEqual else .Equal),
            '<' => self.makeToken(if (self.match('=')) .LessEqual else .Less),
            '>' => self.makeToken(if (self.match('=')) .GreaterEqual else .Greater),

            '\'' => self.string('\''),
            '"' => self.string('"'),
            else => self.errorToken("Unexpected character."),
        };
    }

    inline fn setStart(self: *Self) void {
        self.start = self.start[self.current..];
        self.current = 0;
    }

    fn skipWhitespace(self: *Self) void {
        while (!self.isAtEnd()) {
            switch (self.peek()) {
                // Comments
                '/' => if (self.peekNext() == '/') {
                    _ = self.advance();
                    _ = self.advance();

                    while (self.peek() != '\n' and !self.isAtEnd()) : (_ = self.advance()) {}
                } else return,
                '\n' => {
                    _ = self.advance();
                    self.line += 1;
                },
                ' ', '\r', '\t' => _ = self.advance(),
                else => return,
            }
        }
    }

    inline fn peek(self: *Self) u8 {
        if (self.isAtEnd()) return 0;
        return self.start[self.current];
    }

    inline fn peekNext(self: *Self) u8 {
        if (self.isAtEnd()) return 0;
        return self.start[self.current + 1];
    }

    fn match(self: *Self, expected: u8) bool {
        if (self.isAtEnd()) return false;
        if (self.start[1] != expected) return false;
        self.current += 1;
        return true;
    }

    fn matchAll(self: *Self, expected: []const u8) bool {
        if (self.isAtEnd()) return false;

        for (expected, 1..) |char, idx| {
            if (self.isAtEnd()) return false;
            if (self.start[idx] != char) return false;
        }
        self.current += expected.len;
        return true;
    }

    fn isAlpha(c: u8) bool {
        return (c >= 'a' and c <= 'z') or
            (c >= 'A' and c <= 'Z') or
            c == '_';
    }

    fn isDigit(c: u8) bool {
        return c >= '0' and c <= '9';
    }

    fn isIdentifier(c: u8) bool {
        return isAlpha(c) or isDigit(c);
    }

    fn advance(self: *Self) u8 {
        defer self.current += 1;
        return self.start[self.current];
    }

    fn isAtEnd(self: *Self) bool {
        return self.current >= self.start.len;
    }

    fn makeToken(self: *Self, kind: TokenKind) Token {
        return .{
            .kind = kind,
            .lexeme = self.start[0..self.current],
            .line = self.line,
            .column = @intCast(u32, self.current + 1),
        };
    }

    fn errorToken(self: *Self, msg: []const u8) Token {
        return .{
            .kind = .Error,
            .lexeme = msg,
            .line = self.line,
            .column = @intCast(u32, self.current + 1),
        };
    }

    fn string(self: *Self, end: u8) Token {
        while (self.peek() != end and !self.isAtEnd()) {
            if (self.peek() == '\n') {
                self.line += 1;
            }
            _ = self.advance();
        }

        if (self.isAtEnd()) {
            return self.errorToken("Unterminated string.");
        }

        // Closing quote
        _ = self.advance();
        return self.makeToken(.String);
    }

    fn number(self: *Self) Token {
        while (isDigit(self.peek())) : (_ = self.advance()) {}

        if (self.peek() == '.' and isDigit(self.peekNext())) {
            _ = self.advance();
            while (isDigit(self.peek())) : (_ = self.advance()) {}
        }

        return self.makeToken(.Number);
    }

    fn identifier(self: *Self) Token {
        while (isIdentifier(self.peek())) : (_ = self.advance()) {}
        return self.makeToken(self.identifierType());
    }

    fn identifierType(self: *Self) TokenKind {
        return switch (self.start[0]) {
            'a' => self.checkKeyword(1, "nd", .And),
            'b' => if (self.start.len == 1) .Identifier else switch (self.start[1]) {
                'r' => self.checkKeyword(2, "eak", .Break),
                else => .Identifier,
            },
            'e' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'l' => if (self.start.len == 2) .Identifier else return switch (self.start[2]) {
                    's' => if (self.start.len == 3) .Identifier else return switch (self.start[3]) {
                        'e' => if (self.start.len == 4) .Else else .Identifier,
                        else => .Identifier,
                    },
                    else => .Identifier,
                },
                'x' => self.checkKeyword(2, "port", .Export),
                else => .Identifier,
            },
            'f' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'a' => self.checkKeyword(2, "lse", .False),
                'i' => self.checkKeyword(2, "nal", .Final),
                'o' => self.checkKeyword(2, "r", .For),
                else => .Identifier,
            },
            'i' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'f' => if (self.start.len == 2) .If else .Identifier,
                'n' => if (self.start.len == 2) .In else .Identifier,
                else => .Identifier,
            },
            'l' => self.checkKeyword(1, "et", .Let),
            'n' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'i' => self.checkKeyword(2, "l", .Nil),
                'o' => self.checkKeyword(2, "t", .Not),
                else => .Identifier,
            },
            'o' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'b' => self.checkKeyword(2, "ject", .Object),
                'r' => if (self.start.len == 2) .Or else .Identifier,
                else => .Identifier,
            },
            'r' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'e' => if (self.start.len == 2) .Identifier else switch (self.start[2]) {
                    'c' => if (self.start.len == 3) .Recursive else .Identifier,
                    't' => self.checkKeyword(4, "urn", .Return),
                    else => .Identifier,
                },
                else => .Identifier,
            },
            't' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'h' => self.checkKeyword(2, "en", .Then),
                'r' => self.checkKeyword(2, "ue", .True),
                else => .Identifier,
            },
            'w' => if (self.start.len == 1) .Identifier else return switch (self.start[1]) {
                'h' => if (self.start.len == 2) .Identifier else return switch (self.start[2]) {
                    // 'e' => self.checkKeyword(3, "n", .When),
                    'i' => self.checkKeyword(3, "le", .While),
                    else => .Identifier,
                },
                else => .Identifier,
            },
            else => .Identifier,
        };
    }

    fn checkKeyword(self: *Self, start: usize, rest: []const u8, kind: TokenKind) TokenKind {
        if (self.current != start + rest.len) return .Identifier;

        if (mem.eql(u8, self.start[start..self.current], rest)) {
            return kind;
        }

        return .Identifier;
    }
};
