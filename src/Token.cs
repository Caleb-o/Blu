namespace Blu {
    enum TokenKind {
        Plus, Minus, Star, Slash, Equal,
        Ampersand, Colon, Comma, Dot,
        Semicolon,

        Greater, Less, GreaterEq, LessEq,
        EqualEq,

        LCurly, RCurly,
        LParen, RParen,
        LSquare, RSquare,

        Int, Float, String, True, False,
        
        Identifier, Let, Var, Return, Pub,
        If, Else, While, Struct, Ref, Fn,
        Test, CSharp, Const, Trait,

        Error,
        EndOfFile,
    }

    class Token {
        public TokenKind kind { get; private set; }
        public int line  { get; private set; }
        public int column { get; private set; }
        public string? lexeme { get; private set; }

        public Token(TokenKind kind, int line, int column, string? lexeme) {
            this.kind = kind;
            this.line = line;
            this.column = column;
            this.lexeme = lexeme;
        }

        public override string ToString()
        {
            return (lexeme == null)
                ? string.Empty
                : lexeme;
        }
    }

    sealed class CharToken : Token {
        public CharToken(TokenKind kind, int line, int column)
            : base(kind, line, column, null)
        {}

        public override string ToString()
        {
            return kind switch {
                TokenKind.Plus => "+",
                TokenKind.Minus => "-",
                TokenKind.Star => "*",
                TokenKind.Slash => "/",
                TokenKind.Equal => "=",
                
                TokenKind.Dot => ".",
                TokenKind.Comma => ",",
                TokenKind.Colon => ":",
                TokenKind.Semicolon => ";",

                TokenKind.LParen => "(",
                TokenKind.RParen => ")",
                TokenKind.LCurly => "{",
                TokenKind.RCurly => "}",
                TokenKind.LSquare => "[",
                TokenKind.RSquare => "]",
                
                _ => throw new UnreachableException($"CharToken - {kind}"),
            };
        }
    }
}