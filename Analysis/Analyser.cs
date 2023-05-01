using System;
using System.Collections.Generic;

namespace Blu.Analysis;

sealed class Analyser {
    enum FunctionType {
        None, Function,
    };

    enum Processing {
        None, Object,
    }

    readonly CompilationUnit unit;
    bool hadError = false;
    Processing processing = Processing.None;
    FunctionType function = FunctionType.None;

    readonly Environment environment;
    Environment workingEnvironment;

    public Analyser(CompilationUnit unit) {
        this.unit = unit;
        this.environment = new("MAIN");
        this.workingEnvironment = environment;
        PushScope();
    }

    public bool Analyse() {
        VisitProgram(unit.ast);
        return hadError;
    }

    void PushScope() => workingEnvironment.SymbolTable.Add(new List<BindingSymbol>());

    void PopScope() => workingEnvironment.SymbolTable.RemoveAt(workingEnvironment.SymbolTable.Count - 1);

    void PushEnvironment(string identifier, bool overwrite = false) {
        Environment env = new(identifier, workingEnvironment);
        ((Action<Environment>)(overwrite ? workingEnvironment.AddOrReplace : workingEnvironment.AddIfNone))(env);
        workingEnvironment = env;
    }

    void PopEnvironment() {
        if (workingEnvironment.Parent == null) {
            throw new BluException("Trying to pop into a null environment");
        }

        workingEnvironment = workingEnvironment.Parent;
    }

    public void SoftError(string message, Token token) {
        hadError = true;
        Console.WriteLine($"Error occured: {message} in {unit.fileName} at {token.Line}:{token.Column}");
    }

    void Warning(string message, Token token) {
        Console.WriteLine($"Warning: {message} in {unit.fileName} at {token.Line}:{token.Column}");
    }

    void Visit(AstNode node) {
        switch (node) {
            case ProgramNode n:             VisitProgram(n); break;
            case ExportNode n:              VisitExport(n); break;
            case BodyNode n:                VisitBody(n, true); break;
            case FunctionNode n:            VisitFunction(n); break;
            case BindingNode n:             VisitBinding(n); break;
            case IdentifierNode n:          VisitIdentifier(n); break;
            case FunctionCallNode n:        VisitFunctionCall(n); break;
            case ReturnNode n:              VisitReturn(n); break;
            case PrintNode n:               VisitPrint(n); break;
            case BinaryOpNode n:            VisitBinaryOp(n); break;
            case UnaryOpNode n:             Visit(n.rhs); break;
            case ListLiteralNode n:         VisitListLiteral(n); break;
            case IndexGetNode n:            VisitIndexGet(n); break;
            case PropertyGetNode n:         VisitPropertyGet(n); break;
            case LenNode n:                 Visit(n.Expression); break;
            case ForLoopNode n:             VisitForLoop(n); break;
            case IfNode n:                  VisitIf(n); break;
            case AssignNode n:              VisitAssign(n); break;
            case OrNode n:                  VisitOr(n); break;
            case AndNode n:                 VisitAnd(n); break;
            case EqualityNode n:            VisitEquality(n); break;
            case ComparisonNode n:          VisitComparison(n); break;
            case PrependNode n:             VisitPrepend(n); break;
            case PipeNode n:                VisitPipe(n); break;
            case CloneNode n:               Visit(n.Expression); break;
            case EnvironmentOpenNode n:     VisitEnvironmentOpen(n); break;
            case ObjectNode n: {
                PushScope();
                VisitObject(n);
                PopScope();
                break;
            }

            // Ignore
            case ImportNode:
            case LiteralNode:
                break;
            
            default:
                throw new UnreachableException($"Analyser - Visit ({node})");
        }
    }

    void VisitProgram(ProgramNode node) {
        VisitBody(node.body, false);
    }

    void VisitExport(ExportNode node) {
        HashSet<Span> exported = new();

        if (unit.isMainUnit) {
            SoftError("Cannot export from main module", node.token);
        }

        foreach (var id in node.Identifiers) {
            Span export = id.token.Span;

            if (workingEnvironment.FindSymbol(export) == null) {
                SoftError($"Cannot export item '{export.String()}' which does not exist", id.token);
            }
            
            if (!exported.Add(export)) {
                SoftError($"Item '{export.String()}' has already been exported", id.token);
            }
        }
    }

    void VisitBody(BodyNode node, bool newScope) {
        if (newScope) PushScope();

        foreach (var n in node.statements) {
            Visit(n);
        }

        if (newScope) PopScope();
    }

    void VisitFunction(FunctionNode node) {
        FunctionType oldFunction = function;
        function = FunctionType.Function;

        List<Token> parameters = new();
        HashSet<Span> parameterNames = new();

        PushScope();
        foreach (var param in node.parameters) {
            if (!parameterNames.Add(param.token.Span)) {
                SoftError($"Parameter '{param.token.Span.String()}' has already been defined", param.token);
            }
            parameters.Add(param.token);
            workingEnvironment.DefineSymbol(this, new BindingSymbol(param.token, param.token.Span, true, false));
        }

        Visit(node.body);
        PopScope();

        function = oldFunction;
    }

    void VisitBinding(BindingNode node) {
        var visitExpr = () => {
            if (node.Expression is ObjectNode obj) {
                PushEnvironment(node.token.Span.ToString());
                VisitObject(obj);
                PopEnvironment();
            } else {
                Visit(node.Expression);
            }
        };

        switch (node.Kind) {
            case BindingKind.None: {
                visitExpr();
                workingEnvironment.DefineSymbol(this, new BindingSymbol(node.token, node.token.Span, node.Explicit, false));
                break;
            }

            case BindingKind.Mutable: {
                visitExpr();
                workingEnvironment.DefineSymbol(this, new BindingSymbol(node.token, node.token.Span, node.Explicit, true));
                break;
            }

            case BindingKind.Recursive: {
                workingEnvironment.DefineSymbol(this, new BindingSymbol(node.token, node.token.Span, node.Explicit, false));
                visitExpr();
                break;
            }
        }

        if (workingEnvironment.SymbolTable.Count == 1 && node.token.Span.ToString() == "main" && !node.Explicit) {
            Warning("Main should be marked as explicit", node.token);
        }
    }

    void VisitIdentifier(IdentifierNode node) {
        BindingSymbol? id = workingEnvironment.FindSymbol(node.token.Span);
        if (id == null) {
            SoftError($"Identifier '{node.token.Span.String()}' does not exist", node.token);
        }
    }

    void VisitFunctionCall(FunctionCallNode node) {
        if (node.lhs is ObjectNode) {
            SoftError("Cannot call an object literal", node.token);
        }

        Visit(node.lhs);
        foreach (var arg in node.arguments) {
            Visit(arg);
        }
    }

    void VisitReturn(ReturnNode node) {
        if (function != FunctionType.Function) {
            SoftError("Cannot use return outside of functions", node.token);
        }

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

    void VisitPropertyGet(PropertyGetNode node) => Visit(node.Lhs);

    void VisitForLoop(ForLoopNode node) {
        Visit(node.Start);
        Visit(node.To);

        workingEnvironment.DefineSymbol(this, new BindingSymbol(null, Span.Idx, true, false));
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

        if (node.Lhs is IdentifierNode id) {
            BindingSymbol sym = workingEnvironment.FindSymbol(id.token.Span);
            if (sym == null) {
                SoftError($"Binding '{id.token.Span.String()}' does not exist in any scope", id.token);
                return;
            }
            if (!sym.Mutable) {
                SoftError($"Binding '{id.token.Span.String()}' is not mutable", id.token);
            }
        }
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

    void VisitPipe(PipeNode node) {
        Visit(node.Lhs);

        if (node.Rhs is FunctionCallNode func) {
            AstNode[] newArgs = new AstNode[func.arguments.Length + 1];
            Array.Copy(func.arguments, newArgs, func.arguments.Length);
            newArgs[newArgs.Length - 1] = node.Lhs;

            func.arguments = newArgs;
            VisitFunctionCall(func);
        } else if (node.Rhs is PipeNode pipe && pipe.Lhs is FunctionCallNode func2) {
            AstNode[] newArgs = new AstNode[func2.arguments.Length + 1];
            Array.Copy(func2.arguments, newArgs, func2.arguments.Length);
            newArgs[newArgs.Length - 1] = node.Lhs;

            func2.arguments = newArgs;
            Visit(node.Rhs);
        } else {
            SoftError("Right-hand side of a pipe must be a function call", node.Rhs.token);
        }
    }

    void VisitObject(ObjectNode node) {
        Processing last = processing;
        processing = Processing.Object;

        if (node.Parameters != null) {
            HashSet<string> parameters = new();
            foreach (var (compose, mutable) in node.Parameters) {
                if (!parameters.Add(compose.token.Span.ToString())) {
                    Warning($"Record constructor already contains '{compose.token.Span}'", compose.token);
                }
                workingEnvironment.DefineSymbol(this, new BindingSymbol(compose.token, compose.token.Span, true, mutable));
            }
        }

        if (node.Composed != null) {
            HashSet<string> composed = new();
            foreach (var compose in node.Composed) {
                if (workingEnvironment.FindSymbol(compose.token.Span) == null) {
                    SoftError($"Cannot compose with '{compose.token.Span}' as it does not exist", compose.token);
                }

                if (!composed.Add(compose.token.Span.ToString())) {
                    Warning($"Already composing object with '{compose.token.Span}'", compose.token);
                }
            }
        }

        foreach (var n in node.Inner) {
            Visit(n);
        }

        processing = last;
    }

    void VisitEnvironmentOpen(EnvironmentOpenNode node) {
        FunctionType oldFunction = function;
        function = FunctionType.None;

        PushScope();
        Visit(node.Lhs);

        Environment? env = environment.FindEnv(node.Lhs.token.Span.ToString());
        // environment.DumpInner();
        workingEnvironment.BringIntoScope(env);

        VisitBody(node.Inner, true);
        PopScope();

        function = oldFunction;
    }
}