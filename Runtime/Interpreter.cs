using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Blu.Runtime;

sealed class Interpreter {
    readonly List<Dictionary<string, Value>> bindings = new();
    readonly CompilationUnit Unit;

    static readonly Dictionary<string, Value> CompiledFiles = new();
    static string? basePath;
    string currentPath;

    public Interpreter(CompilationUnit unit) {
        this.Unit = unit;
        this.currentPath = Path.GetDirectoryName(unit.fileName);

        if (basePath == null) {
            basePath = this.currentPath;
        }
    }

    sealed class ReturnEx : Exception {
        public readonly Value value;
        public ReturnEx(Value value) : base() {
            this.value = value;
        }
    }

    public void Run() {
        PushScope();
        _ = Visit(Unit.ast);
        
        if (bindings[0].TryGetValue(Span.Main.ToString(), out var main)) {
            if (main is FunctionValue func) {
                try {
                    _ = Visit(func.Value.body);
                } catch (ReturnEx) {}
            }
        }

        PopScope();
    }

    Value SetBinding(string binding, Value value) {
        for (int i = bindings.Count - 1; i >= 0; --i) {
            if (bindings[i].ContainsKey(binding)) {
                Value oldValue = bindings[i][binding];
                bindings[i][binding] = value;
                return oldValue;
            }
        }

        throw new BluException($"Cannot find binding '{binding}'");
    }

    void DeclareBinding(string binding, Value value) => bindings[bindings.Count - 1].Add(binding, value);


    void DeclareOrOverwriteBinding(string binding, Value value) {
        if (bindings[bindings.Count - 1].ContainsKey(binding)) {
            bindings[bindings.Count - 1][binding] = value;
        } else {
            bindings[bindings.Count - 1].Add(binding, value);
        }
    }

    void PushScope() => bindings.Add(new Dictionary<string, Value>());
    void PopScope() => bindings.RemoveAt(bindings.Count - 1);

    Value FindBinding(string binding) {
        for (int i = bindings.Count - 1; i >= 0; --i) {
            if (bindings[i].ContainsKey(binding)) {
                return bindings[i][binding];
            }
        }

        throw new BluException($"Cannot find binding '{binding}'");
    }

    Value Visit(AstNode node) {
        return node switch {
            ProgramNode n => VisitProgram(n),
            ImportNode n => VisitImport(n),
            ExportNode n => VisitExport(n),
            BodyNode n => VisitBody(n),
            BindingNode n => VisitBinding(n),
            FunctionNode n => VisitFunction(n),
            FunctionCallNode n => VisitFunctionCall(n),
            ReturnNode n => VisitReturn(n),
            PrintNode n => VisitPrint(n),
            IdentifierNode n => VisitIdentifier(n),
            BinaryOpNode n => VisitBinaryOp(n),
            LiteralNode n => VisitLiteral(n),
            ListLiteralNode n => VisitListLiteral(n),
            RecordLiteralNode n => VisitRecordLiteral(n),
            IndexGetNode n => VisitIndexGet(n),
            PropertyGetNode n => VisitPropertyGet(n),
            LenNode n => VisitLen(n),
            ForLoopNode n => VisitForLoop(n),
            IfNode n => VisitIf(n),
            AssignNode n => VisitAssign(n),
            OrNode n => VisitOr(n),
            AndNode n => VisitAnd(n),
            EqualityNode n => VisitEquality(n),
            ComparisonNode n => VisitComparison(n),
            PrependNode n => VisitPrepend(n),
            PipeNode n => VisitPipe(n),
            ClassNode n => VisitClass(n),
            _ => throw new BluException($"Unknown node in interpreter '{node}'"),
        };
    }

    Value VisitProgram(ProgramNode node) => Visit(node.body);

    Value VisitImport(ImportNode node) {
        string oldPath = currentPath;
        string newPath = string.Empty;

        int i = 0;
        foreach (var path in node.Path) {
            newPath += path.token.Span.String().ToString();

            if (i++ < node.Path.Length - 1) {
                newPath += "/";
            }
        }

        newPath = node.FromBase
            ? $"{basePath}/{newPath}.blu"
            : $"{currentPath}/{newPath}.blu";

        if (Interpreter.CompiledFiles.TryGetValue(newPath, out var value)) {
            Console.WriteLine($"Found value at: '{newPath}'");
            return value;
        }
        
        if (Path.Exists(newPath)) {
            CompilationUnit unit;
            currentPath = Path.GetDirectoryName(newPath);
            unit = Program.CompileAndRun(newPath);
            currentPath = oldPath;

            value = new RecordValue(unit.exports);
            Interpreter.CompiledFiles.Add(newPath, value);
        } else {
            throw new BluException($"Cannot find file path '{newPath}'");
        }

        return value;
    }

    Value VisitExport(ExportNode node) {
        foreach (var export in node.Identifiers) {
            string exportName = export.token.Span.String().ToString();
            Unit.exports.Add(exportName, Visit(export));
        }
        return NilValue.The;
    }

    Value VisitBody(BodyNode node) {
        Value value = NilValue.The;
        foreach (var n in node.statements) {
            value = Visit(n);
        }
        return value;
    }

    Value VisitFunction(FunctionNode node) => new FunctionValue(node);

    Value VisitFunctionCall(FunctionCallNode node) {
        Value lhs = Visit(node.lhs);

        if (lhs is FunctionValue func) {
            if (func.Value.parameters.Length != node.arguments.Length) {
                throw new BluException($"Trying to call function with {node.arguments.Length} arguments, but expected {func.Value.parameters.Length}");
            }

            PushScope();
            for (int i = 0; i < func.Value.parameters.Length; ++i) {
                DeclareBinding(func.Value.parameters[i].token.Span.ToString(), Visit(node.arguments[i]));
            }

            Value value = NilValue.The;

            try {
                _ = Visit(func.Value.body);
            } catch (ReturnEx ret) {
                value = ret.value;
            }

            PopScope();

            return value;
        }

        throw new BluException("Trying to call non-function value");
    }

    Value VisitReturn(ReturnNode node) =>
        node.rhs != null
            ? throw new ReturnEx(Visit(node.rhs))
            : NilValue.The;

    Value VisitPrint(PrintNode node) {
        StringBuilder sb = new();

        foreach (var n in node.Arguments) {
            sb.Append(Visit(n));
        }
        Console.WriteLine(sb.ToString());

        return NilValue.The;
    }

    Value VisitBinding(BindingNode node) {
        Value value = Visit(node.expression);
        DeclareOrOverwriteBinding(node.token.Span.ToString(), value);

        return value;
    }

    Value VisitIdentifier(IdentifierNode node) => FindBinding(node.token.Span.ToString());

    Value VisitLiteral(LiteralNode node) {
        return node.token.Kind switch {
            TokenKind.Number => new NumberValue(double.Parse(node.token.Span.String().ToString())),
            TokenKind.String => new StringValue(node.token.Span.String().ToString()),
            TokenKind.True => new BoolValue(true),
            TokenKind.False => new BoolValue(false),
            TokenKind.Nil => NilValue.The,
            _ => throw new UnreachableException("Literal"),
        };
    }

    Value VisitListLiteral(ListLiteralNode node) {
        Value[] values = new Value[node.Expressions.Length];
        for (int i = 0; i < values.Length; ++i) {
            values[i] = Visit(node.Expressions[i]);
        }

        return new ListValue(values);
    }

    Value VisitRecordLiteral(RecordLiteralNode node) {
        Dictionary<string, Value> values = new(node.Values.Length);

        foreach (var (key, item) in node.Values) {
            values[key.token.Span.String().ToString()] = Visit(item);
        }
        return new RecordValue(values);
    }

    Value VisitIndexGet(IndexGetNode node) {
        Value lhs = Visit(node.Lhs);
        Value index = Visit(node.Index);

        if (lhs is ListValue list && index is NumberValue number) {
            int numIndex = (int)number.Value;
            if (numIndex < 0 || numIndex >= list.Values.Length) {
                throw new BluException($"Index '{numIndex}' out of range of '{list.Values.Length}'");
            }
            return list.Values[numIndex];
        }

        throw new BluException("Could not index non-list or use non-number index");
    }

    Value VisitPropertyGet(PropertyGetNode node) {
        Value lhs = Visit(node.Lhs);

        if (lhs is RecordValue record) {
            string property = node.Rhs.token.Span.String().ToString();
            if (record.Properties.TryGetValue(property, out var value)) {
                return value;
            }
            throw new BluException($"Record does not contain property '{property}'");
        }

        throw new BluException("Could not access non-record");
    }

    Value VisitLen(LenNode node) {
        Value item = Visit(node.Expression);

        if (item is ListValue list) {
            return new NumberValue(list.Values.Length);
        }

        throw new BluException($"Cannot index non-list");
    }

    Value VisitForLoop(ForLoopNode node) {
        Value start = Visit(node.Start);
        Value to = Visit(node.To);

        if (start is NumberValue s && to is NumberValue t) {
            int index = (int)s.Value;
            int toIdx = (int)t.Value;

            for (; index < toIdx; ++index) {
                DeclareOrOverwriteBinding(Span.Idx.ToString(), new NumberValue(index));
                Visit(node.Body);
            }

            return NilValue.The;
        }

        throw new BluException("Cannot use non-number values in to range");
    }

    Value VisitIf(IfNode node) {
        Value condition = Visit(node.Condition);
        if (condition is BoolValue b) {
            AstNode? visited = b.Value
                ? node.TrueBody
                : node.FalseBody ?? null;

            if (visited != null) {
                return visited is BodyNode body
                    ? VisitBody(body)
                    : Visit(visited);
            }
            return NilValue.The;
        }

        throw new BluException("Cannot operate if on a non-boolean value");
    }

    Value VisitAssign(AssignNode node) {
        Value rhs = Visit(node.Expression);

        if (node.Lhs is IdentifierNode id) {
            return SetBinding(id.token.Span.ToString(), rhs);
        }

        throw new BluException("Unsupported item in assignment");
    }

    Value VisitOr(OrNode node) {
        Value lhs = Visit(node.Lhs);

        if (lhs is BoolValue l) {
            if (l.Value) { return l; }

            Value rhs = Visit(node.Rhs);
            if (rhs is BoolValue r) {
                if (l.Value) { return l; }
            } else {
                throw new BluException("Can only use or on to booleans");
            }

            return BoolValue.False;
        }

        throw new BluException("Can only use or on to booleans");
    }

    Value VisitAnd(AndNode node) {
        Value lhs = Visit(node.Lhs);
        Value rhs = Visit(node.Rhs);

        if (lhs is BoolValue l && rhs is BoolValue r) {
            return new BoolValue(l.Value && r.Value);
        }

        throw new BluException("Can only use and on to booleans");
    }

    Value VisitEquality(EqualityNode node) {
        Value lhs = Visit(node.Lhs);
        Value rhs = Visit(node.Rhs);

        return node.token.Kind switch {
            TokenKind.EqualEq => lhs.Equal(rhs),
            TokenKind.NotEqual => lhs.NotEqual(rhs),
            _ => throw new BluException("Invalid equality operator"),
        };
    }

    Value VisitComparison(ComparisonNode node) {
        Value lhs = Visit(node.Lhs);
        Value rhs = Visit(node.Rhs);

        return node.token.Kind switch {
            TokenKind.Less => lhs.Less(rhs),
            TokenKind.LessEq => lhs.LessEq(rhs),
            TokenKind.Greater => lhs.Greater(rhs),
            TokenKind.GreaterEq => lhs.GreaterEq(rhs),
            _ => throw new BluException("Invalid comparison operator"),
        };
    }

    Value VisitPrepend(PrependNode node) {
        Value lhs = Visit(node.Lhs);
        Value rhs = Visit(node.Rhs);

        if (rhs is ListValue list) {
            return list.Prepend(lhs);
        }

        throw new BluException("Cannot prepend to non-list");
    }

    Value VisitPipe(PipeNode node) => Visit(node.Rhs);

    Value VisitClass(ClassNode node) {
        Dictionary<string, Value> inner = new();

        if (node.Composed != null) {
            foreach (var compose in node.Composed) {
                string itemName = compose.token.Span.ToString();
                Value composeItem = FindBinding(itemName);

                if (composeItem is RecordValue record) {
                    Dictionary<string, Value> values = new(record.Properties);
                    foreach (var (key, value) in values) {
                        inner[key] = value;
                    }
                } else {
                    throw new BluException($"Cannot compose with non-class item '{itemName}'");
                }
            }
        }

        PushScope();

        foreach (var binding in node.Inner) {
            inner[binding.token.Span.ToString()] = VisitBinding(binding);
        }

        PopScope();
        return new RecordValue(inner);
    }

    Value VisitBinaryOp(BinaryOpNode node) {
        Value lhs = Visit(node.lhs);
        Value rhs = Visit(node.rhs);

        if (lhs.GetType() != rhs.GetType()) {
            throw new BluException($"Expected type '{lhs.GetType()}' in binary op but received '{rhs.GetType()}'");
        }

        return node.token.Kind switch {
            TokenKind.Plus  => lhs.Add(rhs),
            TokenKind.Minus => lhs.Sub(rhs),
            TokenKind.Star  => lhs.Mul(rhs),
            TokenKind.Slash => lhs.Div(rhs),
        };
    }
}