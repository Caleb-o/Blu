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

                    case TokenKind.Struct:
                        StructDefinition(false, GetBody());
                        break;

                    default:
                        Error($"Unknown start of top-level statement '{Kind()}'");
                        break;
                }
            }
        }

        void Statement(BodyNode? body) {
            switch (Kind()) {
                case TokenKind.CSharp:
                    CSharpBlock(body);
                    break;
            }

            Consume(TokenKind.Semicolon, "Expect ';' after statement");
        }

        void PublicStatements() {
            ConsumeAny();

            switch (Kind()) {
                case TokenKind.Fn:
                    FunctionDefinition(true, GetBody());
                    break;

                case TokenKind.Struct:
                    StructDefinition(true, GetBody());
                    break;
                
                default:
                    Error($"Unknown start of public statement '{Kind()}'");
                    break;
            }
        }

        // grammar: csharp string
        void CSharpBlock(BodyNode? body) {
            ConsumeAny();

            Token code = this.current;
            Consume(TokenKind.String, "Expect string after 'csharp' keyword");

            body?.AddNode(new CSharpNode(code));
        }

        void StructDefinition(bool isPublic, BodyNode? body) {
            ConsumeAny(); // We know it's 'struct'

            bool isRef = false;

            if (Kind() == TokenKind.Ref) {
                ConsumeAny();
                isRef = true;
            }

            Token identifier = this.current;
            Consume(TokenKind.Identifier, "Expected identifier after 'struct'/'ref'");

            Consume(TokenKind.LCurly, "Expected '{' after struct identifier");

            List<StructField> fields = new List<StructField>();

            if (Kind() != TokenKind.RCurly) {
                var collectField = () => {
                    List<Token> identifiers = new List<Token>();

                    identifiers.Add(this.current);
                    Consume(TokenKind.Identifier, "Expect identifier in field list");

                    while (Kind() == TokenKind.Comma) {
                        ConsumeAny();

                        identifiers.Add(this.current);
                        Consume(TokenKind.Identifier, "Expect identifier in field list");
                    }

                    Consume(TokenKind.Colon, "Expect ':' after field name(s)");
                    TypeNode type = Type("struct field");

                    Consume(TokenKind.Semicolon, "Expect ';' after field");

                    foreach (var id in identifiers) {
                        fields.Add(new StructField(id, type));
                    }
                };

                collectField();

                while (Kind() != TokenKind.RCurly) {
                    collectField();
                }
            }

            Consume(TokenKind.RCurly, "Expected '}' after struct fields");

            body?.AddNode(new StructNode(identifier, isPublic, isRef, fields.ToArray()));
        }

        void FunctionDefinition(bool isPublic, BodyNode? body) {
            ConsumeAny(); // We know it's 'fn'

            Token identifier = this.current;
            Consume(TokenKind.Identifier, "Expected identifier after 'fn'");

            Consume(TokenKind.LParen, "Expected '(' after function name");

            List<ParameterNode> parameterList = new List<ParameterNode>();

            // Parse 'mut x, y, z: int'
            // Parse 'x, y, z: int'
            // Parse 'x: ref int'
            if (Kind() != TokenKind.RParen) {
                var collectParam = () => {
                    var (isMutable, identifiers) = GetParameter();
                    Consume(TokenKind.Colon, "Expect ':' after parameter identifier");

                    TypeNode type = Type("parameter");

                    foreach (var id in identifiers) {
                        parameterList.Add(new ParameterNode(id, type, isMutable));
                    }
                };

                collectParam();

                while (Kind() == TokenKind.Comma) {
                    ConsumeAny();
                    collectParam();
                }
            }

            Consume(TokenKind.RParen, "Expected ')' after function parameter list");

            // Add function definition to outer body
            body?.AddNode(new FunctionNode(identifier, isPublic, parameterList.ToArray(), Type("parameter list"), Block()));
        }

        BodyNode Block() {
            Consume(TokenKind.LCurly, "Expect '{' to start block");

            BodyNode body = new BodyNode();

            while (Kind() != TokenKind.RCurly) {
                Statement(body);
            }

            Consume(TokenKind.RCurly, "Expect '}' to end block");

            return body;
        }

        TypeNode Type(string where) {
            bool isReference = false;

            if (Kind() == TokenKind.Ref) {
                ConsumeAny();
                isReference = true;
            }

            Token typeName = this.current;
            Consume(TokenKind.Identifier, $"Expected type name after {where}");

            return new TypeNode(typeName, isReference);
        }

        (bool, List<Token>) GetParameter() {
            bool isMutable = false;
            
            if (Kind() == TokenKind.Mutable) {
                ConsumeAny();
                isMutable = true;
            }

            List<Token> parameters = new List<Token>();

            parameters.Add(this.current);
            Consume(TokenKind.Identifier, "Expected parameter identifier");

            while (Kind() == TokenKind.Comma) {
                ConsumeAny();

                parameters.Add(this.current);
                Consume(TokenKind.Identifier, "Expected parameter identifier");
            }

            return (isMutable, parameters);
        }
    }
}