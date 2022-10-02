namespace Blu {
    class Parser {
        Lexer? lexer;
        Token? current;
        CompilationUnit? unit;

        public Parser(string fileName, bool isEntry) {
            // We will throw if the filename is wrong or file is unreadable
            string source = File.ReadAllText(fileName);

            Lexer lexer = new Lexer(source);
            this.current = lexer.Next();
            this.lexer = lexer;
            this.unit = new CompilationUnit(fileName, source, new ProgramNode(), isEntry);
        }

        public CompilationUnit? Parse() {
            TopLevelStatements();
            return this.unit;
        }

        void Error(string message) {
            throw new ParserException(this.unit?.fileName, message, (int)this.current?.line, (int)this.current?.column);
        }

        void Consume(TokenKind kind, string message) {
            if (Kind() == kind) {
                this.current = this.lexer?.Next();
                return;
            }

            Error(message);
        }

        void ConsumeAny() {
            this.current = this.lexer?.Next();
        }
        
        // Get current token's kind
        TokenKind? Kind() => this.current?.kind;
        BodyNode? GetBody() => this.unit?.ast?.body;

        void TopLevelStatements() {
            while (Kind() != TokenKind.EndOfFile) {
                switch (Kind()) {
                    case TokenKind.Pub:
                        PublicStatements();
                        break;

                    case TokenKind.Fn:
                        FunctionDefinition(false, GetBody());
                        break;

                    default:
                        Error($"Unknown start of top-level statement '{Kind()}'");
                        break;
                }
            }
        }

        void PublicStatements() {
            switch (Kind()) {
                case TokenKind.Fn:
                    FunctionDefinition(false, GetBody());
                    break;
                
                default:
                    Error($"Unknown start of public statement '{Kind()}'");
                    break;
            }
        }

        void FunctionDefinition(bool isPublic, BodyNode? body) {
            ConsumeAny(); // We know it's 'fn'

            Token identifier = this.current;
            Consume(TokenKind.Identifier, "Expected identifier after 'fn'");
        }
    }
}