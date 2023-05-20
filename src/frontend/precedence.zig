const TokenKind = @import("lexer.zig").TokenKind;

pub const Precedence = enum {
    // Low to High
    None,
    Low, // Fixed prec marker
    AndOr, // and or
    Not, // not
    Assignment, // = += -= etc
    Equality, // == !=
    Comparison, // < > <= >=
    Term, // + -
    Factor, // * /
    UnaryMinus, // -
    Power, // **
    Call, // . ()
    Unary, // ! +
    Primary,

    pub fn next(self: Precedence) Precedence {
        return @intToEnum(Precedence, @enumToInt(self) + 1);
    }
};

pub fn getPrecedence(tokenType: TokenKind) Precedence {
    return switch (tokenType) {
        // Single-character tokens.
        .LeftParen => .Call,
        .Dot => .Call,
        .Minus, .Plus => .Term,
        .Slash, .Star, .Percent => .Factor,
        .StarStar => .Power,

        // One or two character tokens.
        .BangEqual, .EqualEqual => .Equality,
        .Greater, .GreaterEqual, .Less, .LessEqual => .Comparison,

        // Keywords.
        .And, .Or => .AndOr,

        else => .None,
    };
}
