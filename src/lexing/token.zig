pub const TokenKind = enum(u8) {
    plus, minus, star, slash,
    open_paren, close_paren, open_square, close_square,
    open_brace, close_brace, dot, dot_dot, comma, colon, semi_colon,
    bang, bang_equal, equal, equal_equal, greater, greater_equal,
    less, less_equal, ampersand, pipe, single_quote, double_quote,
    question_mark,

    @"for", @"while", @"if", @"struct", @"fn",
    @"switch", @"null",

    identifier,
    integer, float, string, bool,
    eof,
};

// Take a segment of a file without a new string
pub const Span = struct {
    start: usize,
    end: usize,

    pub fn init(start: usize, end: usize) Span {
        return Span { .start = start, .end = end };
    }
};

pub const Token = struct {
    line: usize,
    column: u16,
    span: Span,
    kind: TokenKind,

    pub fn init(line: usize, column: u16, span: Span, kind: TokenKind) Token {
        return Token { .line = line, .column = column, .span = span, .kind = kind };
    }
};