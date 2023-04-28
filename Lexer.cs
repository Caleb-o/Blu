using System;
using System.Text;

namespace Blu {
    sealed class Lexer {
        string source;
        int line, column, ip;

        public Lexer(string source) {
            this.source = source;
            this.line = 1;
            this.column = 1;
            this.ip = 0;
        }

        public Token Next() {
            if (IsAtEnd()) {
                return MakeEOFToken();
            }

            SkipWhitespace();

            char peeked = Peek();
            if (char.IsLetter(peeked) || peeked == '_') {
                return Identifier();
            }

            if (char.IsDigit(peeked)) {
                return Digit();
            }

            // Character or error
            return peeked switch {
                '+' => MakeCharToken(TokenKind.Plus),
                '-' => MakeCharToken(TokenKind.Minus),
                '*' => MakeCharToken(TokenKind.Star),
                '/' => MakeCharToken(TokenKind.Slash),

                '>' => MatchingCharToken(TokenKind.Greater, (TokenKind.GreaterEq, '=')),
                '<' => MatchingCharToken(TokenKind.Less,
                    (TokenKind.LessEq, '='),
                    (TokenKind.NotEqual, '>')
                ),
                '=' => MatchingCharToken(TokenKind.Equal, (TokenKind.EqualEq, '=')),

                ':' => MakeCharToken(TokenKind.Colon),
                ';' => MakeCharToken(TokenKind.Semicolon),
                '.' => MakeCharToken(TokenKind.Dot),
                ',' => MakeCharToken(TokenKind.Comma),

                '"' => String(),

                '(' => MakeCharToken(TokenKind.LParen),
                ')' => MakeCharToken(TokenKind.RParen),
                '{' => MakeCharToken(TokenKind.LCurly),
                '}' => MakeCharToken(TokenKind.RCurly),
                '[' => MakeCharToken(TokenKind.LSquare),
                ']' => MakeCharToken(TokenKind.RSquare),

                _ => MakeErrorToken($"Unknown character found: '{Peek()}'"),
            };
        }

        Token MakeToken(TokenKind kind, int column, string lexeme) {
            return new Token(kind, this.line, column, lexeme);
        }

        Token MakeEOFToken() {
            return new Token(TokenKind.EndOfFile, this.line, this.column, null);
        }

        Token MakeCharToken(TokenKind kind) {
            Advance();
            return new Token(kind, this.line, this.column - 1, null);
        }

        Token MakeErrorToken(string message) {
            return new Token(TokenKind.Error, this.line, this.column, message);
        }

        Token MatchingCharToken(TokenKind single, params (TokenKind, char)[] matches) {
            char next = PeekNext();

            foreach (var m in matches) {
                if (next == m.Item2) {
                    Advance(); Advance();
                    return new Token(m.Item1, this.line, this.column - 2, null);
                }
            }

            Advance();
            return new Token(single, this.line, this.column - 1, null);
        }

        Token Identifier() {
            int ip = this.ip;
            int column = this.column;
            Advance();

            while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) {
                Advance();
            }

            string lexeme = this.source[ip..this.ip];
            return MakeToken(FindKeyword(lexeme), column, lexeme);
        }

        Token String() {
            Advance();

            int ip = this.ip;
            int column = this.column;

            StringBuilder sb = new StringBuilder();

            while (!IsAtEnd() && Peek() != '"') {
                if (Peek() == '\\') {
                    Advance();
                    sb.Append(Peek());
                    Advance();
                } else {
                    sb.Append(Peek());
                    Advance();
                }
            }
            Advance();

            return MakeToken(TokenKind.String, column, sb.ToString());
        }

        Token Digit() {
            int ip = this.ip;
            int column = this.column;
            bool isFloat = false;

            while (!IsAtEnd() && char.IsDigit(Peek())) {
                Advance();

                // Check if float
                if (Peek() == '.') {
                    if (isFloat) {
                        throw new LexerException("Float already contains a decimal", this.line, this.column);
                    }
                    
                    Advance();
                    isFloat = true;
                }
            }

            return MakeToken(TokenKind.Number, column, this.source[ip..this.ip]);
        }

        TokenKind FindKeyword(ReadOnlySpan<char> lexeme) {
            return lexeme switch {
                "fun" => TokenKind.Fun,
                "return" => TokenKind.Return,

                "for" => TokenKind.For,
                "to" => TokenKind.To,

                "if" => TokenKind.If,
                "then" => TokenKind.Then,
                "else" => TokenKind.Else,
                "print" => TokenKind.Print,

                "let" => TokenKind.Let,
                "nil" => TokenKind.Nil,
                "export" => TokenKind.Export,

                _ => TokenKind.Identifier,
            };
        }

        bool IsAtEnd() {
            return this.ip >= this.source.Length;
        }

        char Peek() {
            return this.source[this.ip];
        }

        char PeekNext() {
            if (this.ip + 1 >= this.source.Length) return '\0';
            return this.source[this.ip + 1];
        }

        void Advance() {
            this.ip++;
            this.column++;
        }

        void AdvanceLine() {
            this.ip++;
            this.line++;
            this.column = 1;
        }

        void SkipWhitespace() {
            while (!IsAtEnd()) {
                switch (Peek()) {
                    // Misc whitespace
                    case char c when (c == ' ' || c == '\t' || c == '\b'): {
                        Advance();
                        break;
                    }

                    // Newlines
                    case '\n':
                        AdvanceLine();
                        break;
                    
                    // Comments
                    case char c when (c == '/' && PeekNext() == '/'): {
                        Advance(); Advance();

                        while (!IsAtEnd() && Peek() != '\n') {
                            Advance();
                        }
                        break;
                    }

                    // Not whitespace, exit
                    default:
                        return;
                }
            }
        }
    }
}