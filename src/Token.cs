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
        Test, CSharp, Const,

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
                ? $"Token {{ {kind}, {line}:{column} }}"
                : $"Token {{ {kind}, {line}:{column}, '{lexeme}' }}";
        }
    }

    sealed class CharToken : Token {
        public CharToken(TokenKind kind, int line, int column)
            : base(kind, line, column, null)
        {}
    }
}