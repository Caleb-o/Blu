using System.Collections.Generic;
using System.IO;

namespace Blu;


sealed class Parser {
    readonly Lexer lexer;
    readonly CompilationUnit unit;
    Token current;

    public Parser(string fileName, bool isEntry) {
        // We will throw if the filename is wrong or file is unreadable
        string source = File.ReadAllText(fileName);

        Lexer lexer = new(source);
        this.current = lexer.Next();
        this.lexer = lexer;
        this.unit = new CompilationUnit(fileName, source, new ProgramNode(), isEntry);
    }

    public CompilationUnit Parse() {
        TopLevelStatements(unit.ast.body);
        return unit;
    }

    void Error(string message) {
        throw new ParserException(unit.fileName, message, (int)current.Line, (int)current.Column);
    }

    void Consume(TokenKind kind, string message) {
        if (current.Kind == kind) {
            current = lexer.Next();
            return;
        }

        Error(message);
    }

    void ConsumeAny() {
        current = lexer.Next();
    }
    
    void TopLevelStatements(BodyNode node) {
        while (current.Kind != TokenKind.EndOfFile) {
            switch (current.Kind) {
                case TokenKind.Let:
                    node.AddNode(BindingDeclaration());
                    break;

                case TokenKind.Export:
                    node.AddNode(ExportDeclaration());
                    break;
                
                default:
                    Error($"Unknown start of top-level statement '{current.Kind}'");
                    break;
            }

            Consume(TokenKind.Semicolon, "Expect ';' after top-level statement");
        }
    }

    AstNode Primary() {
        Token token = current;

        switch (current.Kind) {
            case TokenKind.Number: case TokenKind.String: case TokenKind.Nil:
            case TokenKind.True: case TokenKind.False: {
                ConsumeAny();
                return new LiteralNode(token);
            }

            case TokenKind.LParen: {
                ConsumeAny();
                AstNode expr = Expression();
                Consume(TokenKind.RParen, "Expect ')' after grouped expression");
                return expr;
            }

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

            case TokenKind.Class:
                return ClassDeclaration();
            
            case TokenKind.Print:
                return PrintStatement();

            case TokenKind.Clone:
                return CloneStatement();
            
            case TokenKind.For:
                return ForStatement();
            
            case TokenKind.LSquare:
                return ListLiteral();

            case TokenKind.LCurly:
                return RecordLiteral();

            case TokenKind.Import:
                return Import();

            default:
                Error($"Unknown token found in expression {current.String()}");
                break;
        }

        throw new UnreachableException("Parser - Primary");
    }

    AstNode Call() {
        AstNode node = Primary();

        while (current.Kind.In(TokenKind.LParen, TokenKind.LSquare, TokenKind.Dot, TokenKind.Pipe)) {
            switch (current.Kind) {
                case TokenKind.LParen:
                    node = FunctionCall(node);
                    break;
                
                case TokenKind.LSquare:
                    node = IndexGet(node);
                    break;

                case TokenKind.Dot:
                    node = PropertyGet(node);
                    break;
                
                case TokenKind.Pipe:
                    node = Pipe(node);
                    break;
            }
        }

        return node;
    }

    AstNode Unary() {
        if (current.Kind == TokenKind.Minus) {
            Token op = current;
            ConsumeAny();
            return new UnaryOpNode(op, Call());
        }

        return Call();
    }

    AstNode Prepend() {
        AstNode node = Unary();

        if (current.Kind == TokenKind.At) {
            Token token = current;
            ConsumeAny();
            return new PrependNode(token, node, Expression());
        }

        return node;
    }

    AstNode Factor() {
        AstNode node = Prepend();

        while (current.Kind.In(TokenKind.Star, TokenKind.Slash)) {
            Token op = current;
            ConsumeAny();
            node = new BinaryOpNode(op, node, Prepend());
        }

        return node;
    }

    AstNode Term() {
        AstNode node = Factor();

        while (current.Kind.In(TokenKind.Plus, TokenKind.Minus)) {
            Token op = current;
            ConsumeAny();
            node = new BinaryOpNode(op, node, Factor());
        }

        return node;
    }

    AstNode Comparison() {
        AstNode node = Term();

        while (current.Kind.In(TokenKind.Less, TokenKind.LessEq, TokenKind.Greater, TokenKind.GreaterEq)) {
            Token token = current;
            ConsumeAny();
            node = new ComparisonNode(token, node, Expression());
        }

        return node;
    }

    AstNode Equality() {
        AstNode node = Comparison();

        while (current.Kind.In(TokenKind.EqualEq, TokenKind.NotEqual)) {
            Token token = current;
            ConsumeAny();
            node = new EqualityNode(token, node, Expression());
        }

        return node;
    }

    AstNode And() {
        AstNode node = Equality();

        while (current.Kind == TokenKind.And) {
            Token token = current;
            ConsumeAny();
            node = new OrNode(token, node, Expression());
        }

        return node;
    }

    AstNode Or() {
        AstNode node = And();

        while (current.Kind == TokenKind.Or) {
            Token token = current;
            ConsumeAny();
            node = new OrNode(token, node, Expression());
        }

        return node;
    }

    AstNode Assignment() {
        AstNode node = Or();

        while (current.Kind == TokenKind.LeftArrow) {
            Token token = current;
            ConsumeAny();
            node = new AssignNode(token, node, Expression());
        }

        return node;
    }

    AstNode Expression() => Assignment();

    void Statement(BodyNode body) {
        switch (current.Kind) {
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
        if (current.Kind != TokenKind.Semicolon) {
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
        if (current.Kind == TokenKind.Equal) {
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
        AstNode trueBody = current.Kind == TokenKind.LCurly
            ? Block()
            : Expression();

        AstNode? falseBody = null;
        if (current.Kind == TokenKind.Else) {
            ConsumeAny();
            falseBody = current.Kind == TokenKind.If
                ? IfExpression()
                : current.Kind == TokenKind.LCurly
                    ? Block()
                    : Expression();
        }
        return new IfNode(token, condition, trueBody, falseBody);
    }

    // grammar: let id = expression
    BindingNode BindingDeclaration() {
        ConsumeAny();

        bool exp = false;
        if (current.Kind == TokenKind.Explicit) {
            ConsumeAny();
            exp = true;
        }

        BindingKind kind = BindingKind.None;
        if (current.Kind == TokenKind.Mutable) {
            ConsumeAny();
            kind = BindingKind.Mutable;
        } else if (current.Kind == TokenKind.Rec) {
            ConsumeAny();
            kind = BindingKind.Recursive;
        }

        Token identifier = current;
        Consume(TokenKind.Identifier, "Expect identifier after let");

        if (current.Kind.In(TokenKind.Identifier, TokenKind.LParen)) {
            return FunctionDefinitionFP(identifier, exp, kind);
        } else {
            Consume(TokenKind.Equal, "Expect '=' after identifier");
            return new BindingNode(identifier, exp, kind, Expression());
        }
    }

    AstNode ClassDeclaration() {
        Token token = current;
        ConsumeAny();
        
        List<BindingNode> bindings = new();
        List<IdentifierNode>? composed = null;

        if (current.Kind == TokenKind.Plus) {
            ConsumeAny();
            var collect = () => {
                Token token = current;
                Consume(TokenKind.Identifier, "Expect identifier in class compose list");
                return new IdentifierNode(token);
            };

            composed = new();
            composed.Add(collect());

            while (current.Kind == TokenKind.Comma) {
                ConsumeAny();
                composed.Add(collect());
            }
        }

        Consume(TokenKind.LCurly, "Expect '{' after class keyword");
        while (current.Kind != TokenKind.RCurly) {
            bindings.Add(BindingDeclaration());
            Consume(TokenKind.Semicolon, "Expect ';' after binding");
        }
        Consume(TokenKind.RCurly, "Expect '}' after class declaration");

        return new ClassNode(token, bindings.ToArray(), composed?.ToArray());
    }

    AstNode ExportDeclaration() {
        Token token = current;
        ConsumeAny();

        List<IdentifierNode> exports = new();

        if (current.Kind == TokenKind.Identifier) {
            var collect = () => {
                Token token = current;
                Consume(TokenKind.Identifier, "Expect identifier in export list");
                return new IdentifierNode(token);
            };

            exports.Add(collect());

            while (current.Kind == TokenKind.Comma) {
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

        if (current.Kind != TokenKind.Semicolon) {
            arguments.Add(Expression());

            while (current.Kind == TokenKind.Comma) {
                ConsumeAny();
                arguments.Add(Expression());
            }
        }

        return new PrintNode(token, arguments.ToArray());
    }

    AstNode CloneStatement() {
        Token token = current;
        ConsumeAny();
        return new CloneNode(token, Expression());
    }

    List<IdentifierNode> GetParameterList() {
        List<IdentifierNode> parameterList = new();

        if (current.Kind == TokenKind.LParen) {
            ConsumeAny();
            Consume(TokenKind.RParen, "Expect ')' after '(' in function");
            return parameterList;
        } else if (current.Kind.In(TokenKind.Arrow, TokenKind.Equal)) {
            return parameterList;
        }

        if (!current.Kind.In(TokenKind.Arrow, TokenKind.Equal)) {
            var collect = () => {
                Token token = current;
                Consume(TokenKind.Identifier, "Expect identifier in parameter list");
                return new IdentifierNode(token);
            };

            while (current.Kind == TokenKind.Identifier) {
                parameterList.Add(collect());
            }
        }

        return parameterList;
    }

    BindingNode FunctionDefinitionFP(Token identifier, bool exp, BindingKind kind) {
        List<IdentifierNode> parameters = GetParameterList();

        Consume(TokenKind.Equal, "Expect '=' after parameter list");

        return new BindingNode(
            identifier,
            exp,
            kind,
            new FunctionNode(identifier, parameters.ToArray(), current.Kind == TokenKind.LCurly
                ? Block()
                : new ReturnNode(identifier, Expression()))
        );
    }

    FunctionNode FunctionDefinition() {
        Token token = current;
        ConsumeAny();
        var parameters = GetParameterList();

        Consume(TokenKind.Arrow, "Expect '->' after parameter list");

        AstNode body = current.Kind == TokenKind.LCurly
            ? Block()
            : new ReturnNode(token, Expression());

        return new FunctionNode(token, parameters.ToArray(), body);
    }

    ListLiteralNode ListLiteral() {
        Token token = current;
        ConsumeAny();
        List<AstNode> expressions = new();

        if (current.Kind != TokenKind.RSquare) {
            expressions.Add(Expression());

            while (current.Kind == TokenKind.Comma) {
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

        if (current.Kind == TokenKind.Identifier) {
            var collect = () => {
                IdentifierNode identifier = new(current);
                ConsumeAny();
                Consume(TokenKind.Colon, "Expect ':' after identifier in record");
                return (identifier, Expression());
            };

            values.Add(collect());

            while (current.Kind == TokenKind.Comma) {
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
        if (current.Kind == TokenKind.At) {
            ConsumeAny();
            fromBase = true;
            Consume(TokenKind.Dot, "Expect '.' after '@' in import");
        }

        List<IdentifierNode> path = new() { new IdentifierNode(current) };
        Consume(TokenKind.Identifier, "Expect identifier in import path");

        while (current.Kind == TokenKind.Dot) {
            ConsumeAny();
            path.Add(new IdentifierNode(current));
            Consume(TokenKind.Identifier, "Expect identifier in import path");
        }

        return new ImportNode(token, fromBase, path.ToArray());
    }

    PipeNode Pipe(AstNode node) {
        Token token = current;
        ConsumeAny();
        return new PipeNode(token, node, Expression());
    }

    AstNode FunctionCall(AstNode lhs) {
        ConsumeAny();

        List<AstNode> arguments = new();

        if (current.Kind != TokenKind.RParen) {
            arguments.Add(Expression());

            while(current.Kind == TokenKind.Comma) {
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

        while (current.Kind != TokenKind.RCurly) {
            Statement(newBlock);
        }

        Consume(TokenKind.RCurly, "Expect '}' to end block");

        return newBlock;
    }
}