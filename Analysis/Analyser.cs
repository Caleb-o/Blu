using System;
using System.Collections.Generic;
using System.Linq;

namespace Blu;

sealed class Analyser {
    readonly List<List<Symbol>> symbolTable = new();
    readonly CompilationUnit unit;
    bool hadError = false;

    public Analyser(CompilationUnit unit) {
        this.unit = unit;
        PushScope();
    }

    public bool Analyse() {
        VisitProgram(unit.ast);
        return hadError;
    }

    void PushScope() {
        symbolTable.Add(new List<Symbol>());
    }

    void PopScope() {
        symbolTable.RemoveAt(symbolTable.Count - 1);
    }

    void SoftError(string message, Token token) {
        hadError = true;

        Console.WriteLine($"Error occured: {message} in {this.unit.fileName} at {token.line}:{token.column}");
    }

    Symbol? FindSymbol(string identifier) {
        for (int i = symbolTable.Count - 1; i >= 0; --i) {
            var table = symbolTable[i];

            for (int j = table.Count - 1; j >= 0; --j) {
                if (table[j].identifier == identifier) {
                    return table[j];
                }
            }
        }

        return null;
    }
    void DefineSymbol(Symbol sym) {
        symbolTable[symbolTable.Count - 1].Add(sym);
    }

    void Visit(AstNode node) {
        switch (node) {
            case ProgramNode n:         VisitProgram(n); break;
            case BodyNode n:            VisitBody(n, true); break;
            case FunctionNode n:        VisitFunction(n); break;
            case BindingNode n:         VisitBinding(n); break;
            case IdentifierNode n:      VisitIdentifier(n); break;
            case FunctionCallNode n:    VisitFunctionCall(n); break;
            case ReturnNode n:          VisitReturn(n); break;
            case PrintNode n:           VisitPrint(n); break;
            case BinaryOpNode n:        VisitBinaryOp(n); break;
            case UnaryOpNode n:         Visit(n.rhs); break;
            case ListLiteralNode n:     VisitListLiteral(n); break;
            case IndexGetNode n:        VisitIndexGet(n); break;
            case LenNode n:             Visit(n.Expression); break;
            case ForLoopNode n:         VisitForLoop(n); break;
            case IfNode n:              VisitIf(n); break;
            case AssignNode n:          VisitAssign(n); break;
            case OrNode n:              VisitOr(n); break;
            case AndNode n:             VisitAnd(n); break;
            case EqualityNode n:        VisitEquality(n); break;
            case ComparisonNode n:      VisitComparison(n); break;
            case PrependNode n:         VisitPrepend(n); break;

            case LiteralNode: break;
            
            default:
                throw new UnreachableException($"Analyser - Visit ({node})");
        }
    }

    void VisitProgram(ProgramNode node) {
        VisitBody(node.body, false);
    }

    void VisitBody(BodyNode node, bool newScope) {
        if (newScope) PushScope();

        foreach (var n in node.statements) {
            Visit(n);
        }

        if (newScope) PopScope();
    }

    void VisitFunction(FunctionNode node) {
        List<Token> parameters = new();
        HashSet<string> parameterNames = new();

        PushScope();
        foreach (var param in node.parameters) {
            if (!parameterNames.Add(param.token.lexeme)) {
                SoftError($"Parameter '{param.token.lexeme}' has already been defined", param.token);
            }
            parameters.Add(param.token);
            DefineSymbol(new BindingSymbol(param.token, param.token.lexeme));
        }

        Visit(node.body);
        PopScope();
    }

    void VisitBinding(BindingNode node) {
        DefineSymbol(new BindingSymbol(node.token, node.token.lexeme));
        Visit(node.expression);
    }

    void VisitIdentifier(IdentifierNode node) {
        var id = FindSymbol(node.token.lexeme);
        if (id == null) {
            SoftError($"Identifier '{node.token.lexeme}' does not exist", node.token);
        }
    }

    void VisitFunctionCall(FunctionCallNode node) {
        var id = FindSymbol(node.token.lexeme);
        if (id == null) {
            SoftError($"Identifier '{node.token.lexeme}' does not exist", node.token);
        }

        foreach (var arg in node.arguments) {
            Visit(arg);
        }
    }

    void VisitReturn(ReturnNode node) {
        Utils.RunNonNull(node.rhs, (rhs) => Visit((AstNode)rhs));
    }

    void VisitPrint(PrintNode node) {
        foreach (var n in node.Arguments) {
            Visit(n);
        }
    }

    void VisitBinaryOp(BinaryOpNode node) {
        Visit(node.lhs);
        Visit(node.rhs);
    }

    void VisitListLiteral(ListLiteralNode node) {
        foreach (var n in node.Expressions) {
            Visit(n);
        }
    }

    void VisitIndexGet(IndexGetNode node) {
        Visit(node.Lhs);
        Visit(node.Index);
    }

    void VisitForLoop(ForLoopNode node) {
        Visit(node.Start);
        Visit(node.To);

        DefineSymbol(new BindingSymbol(null, "idx"));
        Visit(node.Body);
    }

    void VisitIf(IfNode node) {
        Visit(node.Condition);
        Visit(node.TrueBody);

        if (node.FalseBody != null) {
            Visit(node.FalseBody);
        }
    }

    void VisitAssign(AssignNode node) {
        Visit(node.Lhs);
        Visit(node.Expression);
    }

    void VisitOr(OrNode node) {
        Visit(node.Lhs);
        Visit(node.Rhs);
    }

    void VisitAnd(AndNode node) {
        Visit(node.Lhs);
        Visit(node.Rhs);
    }

    void VisitEquality(EqualityNode node) {
        Visit(node.Lhs);
        Visit(node.Rhs);
    }

    void VisitComparison(ComparisonNode node) {
        Visit(node.Lhs);
        Visit(node.Rhs);
    }

    void VisitPrepend(PrependNode node) {
        Visit(node.Lhs);
        Visit(node.Rhs);
    }
}