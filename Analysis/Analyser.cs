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
        VisitProgram(this.unit.ast);
        return this.hadError;
    }

    void PushScope() {
        symbolTable.Add(new List<Symbol>());
    }

    void PopScope() {
        symbolTable.Remove(symbolTable.Last());
    }

    void SoftError(string message, Token token) {
        hadError = true;

        Console.WriteLine($"Error occured: {message} in {this.unit.fileName} at {token.line}:{token.column}");
    }

    // Note: This error will cause the compilation to stop entirely
    void Error(string message, Token token) {
        throw new ParserException(this.unit.fileName, message, (int)token.line, (int)token.column);
    }

    Symbol? FindSymbol(string identifier) {
        for (int i = symbolTable.Count - 1; i >= 0; --i) {
            var table = symbolTable[i];

            for (int j = 0; j < table.Count; j++) {
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
            case Return n:              VisitReturn(n); break;

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

        foreach (var param in node.parameters) {
            if (!parameterNames.Add(param.token.lexeme)) {
                SoftError($"Parameter '{param.token.lexeme}' has already been defined", param.token);
            }
            parameters.Add(param.token);
        }

        PushScope();
        VisitBody(node.body, false);
        PopScope();
    }

    void VisitBinding(BindingNode node) {
        DefineSymbol(new BindingSymbol(node.token));
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

    void VisitReturn(Return node) {
        Utils.RunNonNull(node.rhs, (rhs) => Visit((AstNode)rhs));
    }
}