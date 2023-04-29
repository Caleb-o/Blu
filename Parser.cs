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
            if (current.kind == kind) {
                current = lexer.Next();
                return;
            }

            Error(message);
        }

        void ConsumeAny() {
            this.current = this.lexer.Next();
        }
        
        void TopLevelStatements(BodyNode node) {
            while (current.kind != TokenKind.EndOfFile) {
                switch (current.kind) {
                    case TokenKind.Let:
                        node.AddNode(BindingDeclaration());
                        break;
                    
                    default:
                        Error($"Unknown start of top-level statement '{current.kind}'");
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
                
                case TokenKind.Len: {
                    ConsumeAny();
                    return new LenNode(token, Expression());
                }

                case TokenKind.Let:
                    return BindingDeclaration();
                
                case TokenKind.Print:
                    return PrintStatement();
                
                case TokenKind.For:
                    return ForStatement();
                
                case TokenKind.LSquare:
                    return ListLiteral();

                default:
                    Error($"Unknown token found in expression {current.lexeme}");
                    break;
            }

            throw new UnreachableException("Parser - Primary");
        }

        AstNode Call() {
            AstNode node = Primary();

            while (current.kind.In(TokenKind.LParen, TokenKind.LSquare)) {
                switch (current.kind) {
                    case TokenKind.LParen:
                        node = FunctionCall(node);
                        break;
                    
                    case TokenKind.LSquare:
                        node = IndexGet(node);
                        break;
                }
            }

            return node;
        }

        AstNode Unary() {
            if (current.kind == TokenKind.Minus) {
                Token op = current;
                ConsumeAny();
                return new UnaryOpNode(op, Call());
            }

            return Call();
        }

        AstNode Factor() {
            AstNode node = Unary();

            while (current.kind.In(TokenKind.Star, TokenKind.Slash)) {
                Token op = current;
                ConsumeAny();
                node = new BinaryOpNode(op, node, Unary());
            }

            return node;
        }

        AstNode Term() {
            AstNode node = Factor();

            while (current.kind.In(TokenKind.Plus, TokenKind.Minus)) {
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
            switch (current.kind) {
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
            if (current.kind != TokenKind.Semicolon) {
                rhs = Expression();
            }
            body.statements.Add(new ReturnNode(token, rhs));
        }

        AstNode ForStatement() {
            Token token = current;
            ConsumeAny();

            AstNode start = Expression();
            Consume(TokenKind.To, "Expect 'to' after expression");
            AstNode to = Expression();

            AstNode inner;
            if (current.kind == TokenKind.Equal) {
                ConsumeAny();
                inner = Expression();
            } else {
                inner = Block();
            }
            
            return new ForLoopNode(token, start, to, inner);
        }

        // grammar: (let|var) id = expression
        AstNode BindingDeclaration() {
            ConsumeAny();

            Token identifier = current;
            Consume(TokenKind.Identifier, "Expect identifier after let");

            if (current.kind.In(TokenKind.Identifier, TokenKind.LParen)) {
                return FunctionDefinitionFP(identifier);
            } else {
                Consume(TokenKind.Equal, "Expect '=' after identifier");
                return new BindingNode(identifier, Expression());
            }
        }

        AstNode PrintStatement() {
            Token token = current;
            ConsumeAny();

            List<AstNode> arguments = new();

            if (current.kind != TokenKind.Semicolon) {
                arguments.Add(Expression());

                while (current.kind == TokenKind.Comma) {
                    ConsumeAny();
                    arguments.Add(Expression());
                }
            }

            return new PrintNode(token, arguments.ToArray());
        }

        List<IdentifierNode> GetParameterList() {
            List<IdentifierNode> parameterList = new();

            if (current.kind == TokenKind.LParen) {
                ConsumeAny();
                Consume(TokenKind.RParen, "Expect ')' after '(' in function");
                return parameterList;
            }

            if (current.kind.In(TokenKind.Arrow, TokenKind.Equal)) {
                return parameterList;
            }

            if (!current.kind.In(TokenKind.Arrow, TokenKind.Equal)) {
                var collect = () => {
                    Token token = current;
                    Consume(TokenKind.Identifier, "Expect identifier in parameter list");
                    return new IdentifierNode(token);
                };

                while (current.kind == TokenKind.Identifier) {
                    parameterList.Add(collect());
                }
            }

            return parameterList;
        }

        AstNode FunctionDefinitionFP(Token identifier) {
            List<IdentifierNode> parameters = GetParameterList();

            Consume(TokenKind.Equal, "Expect '=' after parameter list");

            return new BindingNode(
                identifier,
                new FunctionNode(identifier, parameters.ToArray(), current.kind == TokenKind.LCurly
                    ? Block()
                    : new ReturnNode(identifier, Expression()))
            );
        }

        FunctionNode FunctionDefinition() {
            Token token = current;
            ConsumeAny();
            var parameters = GetParameterList();

            Consume(TokenKind.Arrow, "Expect '->' after parameter list");

            AstNode body = current.kind == TokenKind.LCurly
                ? Block()
                : new ReturnNode(token, Expression());

            return new FunctionNode(token, parameters.ToArray(), body);
        }

        ListLiteralNode ListLiteral() {
            Token token = current;
            ConsumeAny();
            List<AstNode> expressions = new();

            if (current.kind != TokenKind.RSquare) {
                expressions.Add(Expression());

                while (current.kind == TokenKind.Comma) {
                    ConsumeAny();
                    expressions.Add(Expression());
                }
            }

            Consume(TokenKind.RSquare, "Expect ']' after list literal");
            return new ListLiteralNode(token, expressions.ToArray());
        }

        AstNode FunctionCall(AstNode lhs) {
            ConsumeAny();

            List<AstNode> arguments = new();

            if (current.kind != TokenKind.RParen) {
                arguments.Add(Expression());

                while(current.kind == TokenKind.Comma) {
                    ConsumeAny();
                    arguments.Add(Expression());
                }
            }

            Consume(TokenKind.RParen, "Expect ')' after argument list");
            return new FunctionCallNode(lhs.token, lhs, arguments.ToArray());
        }
        
        AstNode IndexGet(AstNode lhs) {
            Token token = current;
            ConsumeAny();

            AstNode index = Expression();
            Consume(TokenKind.RSquare, "Expect ']' after index");
            return new IndexGetNode(token, lhs, index);
        }

        BodyNode Block() {
            Consume(TokenKind.LCurly, "Expect '{' to start block");

            BodyNode newBlock = new();

            while (current.kind != TokenKind.RCurly) {
                Statement(newBlock);
            }

            Consume(TokenKind.RCurly, "Expect '}' to end block");

            return newBlock;
        }
    }
}