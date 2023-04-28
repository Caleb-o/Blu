using System.Collections.Generic;
using System.IO;

namespace Blu {
    sealed class Parser {
        Lexer lexer;
        Token current;
        CompilationUnit unit;

        public Parser(string fileName, bool isEntry) {
            // We will throw if the filename is wrong or file is unreadable
            string source = File.ReadAllText(fileName);

            Lexer lexer = new Lexer(source);
            this.current = lexer.Next();
            this.lexer = lexer;
            this.unit = new CompilationUnit(fileName, source, new ProgramNode(), isEntry);
        }

        public CompilationUnit Parse() {
            TopLevelStatements(unit.ast.body);
            return unit;
        }

        void Error(string message) {
            throw new ParserException(unit.fileName, message, (int)current.line, (int)current.column);
        }

        void Consume(TokenKind kind, string message) {
            if (Kind() == kind) {
                current = lexer.Next();
                return;
            }

            Error(message);
        }

        void ConsumeAny() {
            this.current = this.lexer.Next();
        }
        
        // Get current token's kind
        TokenKind Kind() => current.kind;

        void TopLevelStatements(BodyNode node) {
            while (current.kind != TokenKind.EndOfFile) {
                switch (Kind()) {
                    case TokenKind.Let:
                        BindingDeclaration(node);
                        break;
                    
                    default:
                        Error($"Unknown start of top-level statement '{Kind()}'");
                        break;
                }

                Consume(TokenKind.Semicolon, "Expect ';' after top-level statement");
            }
        }

        AstNode Primary() {
            Token token = current;

            switch (current.kind) {
                case TokenKind.Number: case TokenKind.String: case TokenKind.Nil:
                case TokenKind.True: case TokenKind.False: {
                    ConsumeAny();
                    return new LiteralNode(token);
                }

                // Identifier
                case TokenKind.Identifier:
                    ConsumeAny();
                    return new IdentifierNode(token);

                case TokenKind.Fun:
                    return FunctionDefinition();

                default:
                    Error($"Unknown token found in expression {current.lexeme}");
                    break;
            }

            throw new UnreachableException("Parser - Primary");
        }

        AstNode Call() {
            AstNode node = Primary();

            while (Kind() == TokenKind.LParen) {
                node = FunctionCall(node);
            }

            return node;
        }

        AstNode Unary() {
            if (Kind() == TokenKind.Minus) {
                Token op = current;
                ConsumeAny();
                return new UnaryOpNode(op, Call());
            }

            return Call();
        }

        AstNode Factor() {
            AstNode node = Unary();

            while (Kind().In(TokenKind.Star, TokenKind.Slash)) {
                Token op = current;
                ConsumeAny();
                node = new BinaryOpNode(op, node, Unary());
            }

            return node;
        }

        AstNode Term() {
            AstNode node = Factor();

            while (Kind().In(TokenKind.Plus, TokenKind.Minus)) {
                Token op = current;
                ConsumeAny();
                node = new BinaryOpNode(op, node, Factor());
            }

            return node;
        }

        AstNode Expression() {
            return Term();
        }

        void Statement(BodyNode body) {
            switch (Kind()) {
                case TokenKind.Let:
                    BindingDeclaration(body);
                    break;
                
                case TokenKind.Print:
                    PrintStatement(body);
                    break;
                
                case TokenKind.Return:
                    ReturnStatement(body);
                    break;
                
                default:
                    body.AddNode(Expression());
                    break;
            }

            Consume(TokenKind.Semicolon, "Expect ';' after statement");
        }

        void ReturnStatement(BodyNode body) {
            Token token = current;
            ConsumeAny();

            AstNode? rhs = null;
            if (Kind() != TokenKind.Semicolon) {
                rhs = Expression();
            }
            body.statements.Add(new ReturnNode(token, rhs));
        }

        // grammar: (let|var) id = expression
        void BindingDeclaration(BodyNode body) {
            ConsumeAny();

            Token identifier = current;
            Consume(TokenKind.Identifier, "Expect identifier after let");

            if (Kind() == TokenKind.Identifier) {
                body.AddNode(FunctionDefinitionFP(identifier));
            } else {
                Consume(TokenKind.Equal, "Expect '=' after identifier");
                body.AddNode(new BindingNode(identifier, Expression()));
            }
        }

        void PrintStatement(BodyNode node) {
            Token token = current;
            ConsumeAny();

            List<AstNode> arguments = new();

            if (Kind() != TokenKind.Semicolon) {
                arguments.Add(Expression());

                while (Kind() == TokenKind.Comma) {
                    ConsumeAny();
                    arguments.Add(Expression());
                }
            }

            node.AddNode(new PrintNode(token, arguments.ToArray()));
        }

        List<IdentifierNode> GetParameterList() {
            List<IdentifierNode> parameterList = new();

            if (current.kind != TokenKind.LParen) {
                return parameterList;
            }

            Consume(TokenKind.LParen, "Expected '(' after function");

            if (Kind() != TokenKind.RParen) {
                var collect = () => {
                    Token token = current;
                    Consume(TokenKind.Identifier, "Expect identifier in parameter list");
                    return new IdentifierNode(token);
                };

                parameterList.Add(collect());

                while (Kind() == TokenKind.Comma) {
                    ConsumeAny();
                    parameterList.Add(collect());
                }
            }

            Consume(TokenKind.RParen, "Expected ')' after function parameter list");
            return parameterList;
        }

        AstNode FunctionDefinitionFP(Token identifier) {
            List<IdentifierNode> parameters = new();

            var collect = () => {
                Token token = current;
                Consume(TokenKind.Identifier, "Expect identifier in parameter list");
                return new IdentifierNode(token);
            };

            while (Kind() == TokenKind.Identifier) {
                parameters.Add(collect());
            }

            Consume(TokenKind.Equal, "Expect '=' after parameter list");

            return new BindingNode(identifier, new FunctionNode(identifier, parameters.ToArray(), Block()));
        }

        FunctionNode FunctionDefinition() {
            Token token = current;
            ConsumeAny();
            var parameters = GetParameterList();

            return new FunctionNode(token, parameters.ToArray(), Block());
        }

        AstNode FunctionCall(AstNode lhs) {
            Consume(TokenKind.LParen, "Expect '(' before argument list");

            List<AstNode> arguments = new();

            if (Kind() != TokenKind.RParen) {
                arguments.Add(Expression());

                while(Kind() == TokenKind.Comma) {
                    ConsumeAny();
                    arguments.Add(Expression());
                }
            }

            Consume(TokenKind.RParen, "Expect ')' after argument list");
            return new FunctionCallNode(lhs.token, lhs, arguments.ToArray());
        }

        BodyNode Block() {
            Consume(TokenKind.LCurly, "Expect '{' to start block");

            BodyNode newBlock = new();

            while (Kind() != TokenKind.RCurly) {
                Statement(newBlock);
            }

            Consume(TokenKind.RCurly, "Expect '}' to end block");

            return newBlock;
        }
    }
}