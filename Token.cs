namespace Blu {
    enum TokenKind {
        Plus, Minus, Star, Slash, Equal,
        Colon, Comma, Dot, Semicolon,

        Greater, Less, GreaterEq, LessEq,
        NotEqual, EqualEq,

        LCurly, RCurly,
        LParen, RParen,
        LSquare, RSquare,

        String, Number, True, False, Nil,
        
        Identifier, Let, Return,
        If, Then, Else, Fun, For, To,
        Export, Print,// Exports identifiers into an object, which can be imported

        Error,
        EndOfFile,
    }

    sealed class Token {
        public readonly TokenKind kind;
        public readonly int line;
        public readonly int column;
        public readonly string? lexeme;

        public Token(TokenKind kind, int line, int column, string? lexeme) {
            this.kind = kind;
            this.line = line;
            this.column = column;
            this.lexeme = lexeme;
        }

        public override string ToString() => lexeme ?? string.Empty;
    }
}