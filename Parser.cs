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
            current = lexer.Next();
        }
        
        void TopLevelStatements(BodyNode node) {
            while (current.kind != TokenKind.EndOfFile) {
                switch (current.kind) {
                    case TokenKind.Let:
                        node.AddNode(BindingDeclaration());
                        break;

                    case TokenKind.Export:
                        node.AddNode(ExportDeclaration());
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

                case TokenKind.If:
                    return IfExpression();

                case TokenKind.Let:
                    return BindingDeclaration();
                
                case TokenKind.Print:
                    return PrintStatement();
                
                case TokenKind.For:
                    return ForStatement();
                
                case TokenKind.LSquare:
                    return ListLiteral();

                case TokenKind.LCurly:
                    return RecordLiteral();

                case TokenKind.Import:
                    return Import();

                default:
                    Error($"Unknown token found in expression {current.lexeme}");
                    break;
            }

            throw new UnreachableException("Parser - Primary");
        }

        AstNode Call() {
            AstNode node = Primary();

            while (current.kind.In(TokenKind.LParen, TokenKind.LSquare, TokenKind.Dot)) {
                switch (current.kind) {
                    case TokenKind.LParen:
                        node = FunctionCall(node);
                        break;
                    
                    case TokenKind.LSquare:
                        node = IndexGet(node);
                        break;

                    case TokenKind.Dot:
                        node = PropertyGet(node);
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

        AstNode Prepend() {
            AstNode node = Unary();

            if (current.kind == TokenKind.At) {
                Token token = current;
                ConsumeAny();
                return new PrependNode(token, node, Expression());
            }

            return node;
        }

        AstNode Factor() {
            AstNode node = Prepend();

            while (current.kind.In(TokenKind.Star, TokenKind.Slash)) {
                Token op = current;
                ConsumeAny();
                node = new BinaryOpNode(op, node, Prepend());
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

        AstNode Comparison() {
            AstNode node = Term();

            while (current.kind.In(TokenKind.Less, TokenKind.LessEq, TokenKind.Greater, TokenKind.GreaterEq)) {
                Token token = current;
                ConsumeAny();
                node = new ComparisonNode(token, node, Expression());
            }

            return node;
        }

        AstNode Equality() {
            AstNode node = Comparison();

            while (current.kind.In(TokenKind.EqualEq, TokenKind.NotEqual)) {
                Token token = current;
                ConsumeAny();
                node = new EqualityNode(token, node, Expression());
            }

            return node;
        }

        AstNode And() {
            AstNode node = Equality();

            while (current.kind == TokenKind.And) {
                Token token = current;
                ConsumeAny();
                node = new OrNode(token, node, Expression());
            }

            return node;
        }

        AstNode Or() {
            AstNode node = And();

            while (current.kind == TokenKind.Or) {
                Token token = current;
                ConsumeAny();
                node = new OrNode(token, node, Expression());
            }

            return node;
        }

        AstNode Assignment() {
            AstNode node = Or();

            while (current.kind == TokenKind.LeftArrow) {
                Token token = current;
                ConsumeAny();
                node = new AssignNode(token, node, Expression());
            }

            return node;
        }

        AstNode Expression() => Assignment();

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

        AstNode IfExpression() {
            Token token = current;
            ConsumeAny();

            AstNode condition = Expression();
            Consume(TokenKind.Then, "Expect 'then' after if condition");
            AstNode trueBody = current.kind == TokenKind.LCurly
                ? Block()
                : Expression();

            AstNode? falseBody = null;
            if (current.kind == TokenKind.Else) {
                ConsumeAny();
                falseBody = current.kind == TokenKind.If
                    ? IfExpression()
                    : current.kind == TokenKind.LCurly
                        ? Block()
                        : Expression();
            }
            return new IfNode(token, condition, trueBody, falseBody);
        }

        // grammar: let id = expression
        AstNode BindingDeclaration() {
            ConsumeAny();

            BindingKind kind = BindingKind.None;
            if (current.kind == TokenKind.Mutable) {
                ConsumeAny();
                kind = BindingKind.Mutable;
            } else if (current.kind == TokenKind.Rec) {
                ConsumeAny();
                kind = BindingKind.Recursive;
            }

            Token identifier = current;
            Consume(TokenKind.Identifier, "Expect identifier after let");

            if (current.kind.In(TokenKind.Identifier, TokenKind.LParen)) {
                return FunctionDefinitionFP(identifier, kind);
            } else {
                Consume(TokenKind.Equal, "Expect '=' after identifier");
                return new BindingNode(identifier, kind, Expression());
            }
        }

        AstNode ExportDeclaration() {
            Token token = current;
            ConsumeAny();

            List<IdentifierNode> exports = new();

            if (current.kind == TokenKind.Identifier) {
                var collect = () => {
                    Token token = current;
                    Consume(TokenKind.Identifier, "Expect identifier in export list");
                    return new IdentifierNode(token);
                };

                exports.Add(collect());

                while (current.kind == TokenKind.Comma) {
                    ConsumeAny();
                    exports.Add(collect());
                }
            }

            return new ExportNode(token, exports.ToArray());
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
            } else if (current.kind.In(TokenKind.Arrow, TokenKind.Equal)) {
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

        AstNode FunctionDefinitionFP(Token identifier, BindingKind kind) {
            List<IdentifierNode> parameters = GetParameterList();

            Consume(TokenKind.Equal, "Expect '=' after parameter list");

            return new BindingNode(
                identifier,
                kind,
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

        RecordLiteralNode RecordLiteral() {
            Token token = current;
            ConsumeAny();

            List<(IdentifierNode, AstNode)> values = new();

            if (current.kind == TokenKind.Identifier) {
                var collect = () => {
                    IdentifierNode identifier = new(current);
                    ConsumeAny();
                    Consume(TokenKind.Colon, "Expect ':' after identifier in record");
                    return (identifier, Expression());
                };

                values.Add(collect());

                while (current.kind == TokenKind.Comma) {
                    ConsumeAny();
                    values.Add(collect());
                }
            }
            Consume(TokenKind.RCurly, "Expect '}' after record literal");

            return new RecordLiteralNode(token, values.ToArray());
        }

        ImportNode Import() {
            Token token = current;
            ConsumeAny();

            bool fromBase = false;
            if (current.kind == TokenKind.At) {
                ConsumeAny();
                fromBase = true;
                Consume(TokenKind.Dot, "Expect '.' after '@' in import");
            }

            List<IdentifierNode> path = new() { new IdentifierNode(current) };
            Consume(TokenKind.Identifier, "Expect identifier in import path");

            while (current.kind == TokenKind.Dot) {
                ConsumeAny();
                path.Add(new IdentifierNode(current));
                Consume(TokenKind.Identifier, "Expect identifier in import path");
            }

            return new ImportNode(token, fromBase, path.ToArray());
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

        AstNode PropertyGet(AstNode lhs) {
            Token token = current;
            ConsumeAny();

            Token identifier = current;
            Consume(TokenKind.Identifier, "Expect identifier after '.'");
            
            return new PropertyGetNode(token, lhs, new IdentifierNode(identifier));
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