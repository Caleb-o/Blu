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
    RecordValue? currentRecord;

    sealed class StackFrame {
        public readonly StackFrame? Parent;
        public readonly string Identifier;
        public readonly (string, Value)[]? Parameters;

        public StackFrame(StackFrame? parent, string identifier, (string, Value)[]? parameters) {
            this.Parent = parent;
            this.Identifier = identifier;
            this.Parameters = parameters;
        }
    }

    StackFrame TopFrame = new(null, "<script>", null);

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

        RegisterBuiltins();

        try {
            _ = Visit(Unit.ast);
            
            if (bindings[0].TryGetValue(Span.Main.ToString(), out var main)) {
                if (main is FunctionValue func) {
                    try {
                        PushFrame("main", null);
                        _ = Visit(func.Value.body);
                        PopFrame();
                    } catch (ReturnEx) {}
                }
            }
        } catch (BluException be) {
            Console.WriteLine(be.Message);
            DumpStackTrace();
        }

        PopScope();
    }

    void RegisterBuiltins() {
        Dictionary<string, Value> builtinValues = new();
        RecordValue builtin = new(null, builtinValues);

        DeclareBinding("builtin", builtin);

        foreach (var (id, mod) in Internal.Builtins.Modules) {
            Dictionary<string, Value> recordValues = new();
            RecordValue record = new(null, recordValues);
            builtinValues.Add(id, record);

            foreach (var (fid, func) in mod.Functions) {
                recordValues.Add(fid, new NativeFunctionValue(func));
            }
        }
    }

    void DumpStackTrace() {
        Console.WriteLine($"=== Stack Trace :: '{Unit.fileName}' ===");
        StackFrame? top = TopFrame;
        while (top != null) {
            Console.Write($"to {top.Identifier}(");

            for (int i = 0; i < top.Parameters?.Length; ++i) {
                Console.Write($"{top.Parameters[i].Item1}:{top.Parameters[i].Item2}");

                if (i < top.Parameters?.Length - 1) {
                    Console.Write(", ");
                }
            }
            Console.WriteLine(')');
            top = top.Parent;
        }
    }

    Value SetBinding(string binding, Value value) {
        for (int i = bindings.Count - 1; i >= 0; --i) {
            if (bindings[i].ContainsKey(binding)) {
                Value oldValue = bindings[i][binding];
                bindings[i][binding] = value;
                return oldValue;
            }
        }

        throw new BluException($"Cannot find binding '{binding}' to set");
    }

    void DeclareBinding(string binding, Value value) => bindings[bindings.Count - 1].Add(binding, value);


    void DeclareOrOverwriteBinding(string binding, Value value) =>
        bindings[bindings.Count - 1][binding] = value;

    void PushFrame(string identifier, (string, Value)[]? parameters) {
        StackFrame frame = new(TopFrame, identifier, parameters);
        TopFrame = frame;
    }

    void PopFrame() {
        if (TopFrame.Parent == null) {
            throw new BluException("Top frame has an invalid parent frame");
        }
        TopFrame = TopFrame.Parent;
    }

    void PushScope() => bindings.Add(new Dictionary<string, Value>());
    void PopScope() => bindings.RemoveAt(bindings.Count - 1);

    Value? FindBinding(string binding) {
        for (int i = bindings.Count - 1; i >= 0; --i) {
            if (bindings[i].ContainsKey(binding)) {
                Value value = bindings[i][binding];

                if (value is RecordValue record) {
                    currentRecord = record;
                }

                return value;
            }
        }

        // Search in current record
        if (currentRecord.Properties.TryGetValue(binding, out var val)) {
            return val;
        }

        return null;
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
            ObjectNode n => VisitObject(n),
            CloneNode n => VisitClone(n),
            EnvironmentOpenNode n => VisitEnvironmentOpen(n),
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

        newPath = node.Kind switch {
            ImportKind.Base => $"{basePath}/{newPath}.blu",
            ImportKind.Normal => $"{currentPath}/{newPath}.blu",
            ImportKind.Std => $"{AppDomain.CurrentDomain.BaseDirectory}std/{newPath}.blu",
        };

        if (Interpreter.CompiledFiles.TryGetValue(newPath, out var value)) {
            Console.WriteLine($"Found value at: '{newPath}'");
            return value;
        }
        
        if (Path.Exists(newPath)) {
            CompilationUnit unit;
            currentPath = Path.GetDirectoryName(newPath);
            unit = Program.CompileAndRun(newPath, false);
            currentPath = oldPath;

            value = new RecordValue(null, unit.exports);
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

        switch (lhs) {
            case FunctionValue func: {
                if (func.Value.parameters.Length != node.arguments.Length) {
                    throw new BluException($"Trying to call function '{func.Value.token.Span}' with {node.arguments.Length} arguments, but expected {func.Value.parameters.Length}");
                }

                (string, Value)[]? parameters = null;

                PushScope();
                if (func.Value.parameters.Length > 0) {
                    parameters = new (string, Value)[func.Value.parameters.Length];

                    for (int i = 0; i < func.Value.parameters.Length; ++i) {
                        string binding = func.Value.parameters[i].token.Span.ToString();
                        Value arg = Visit(node.arguments[i]);

                        parameters[i] = (binding, arg);

                        DeclareBinding(binding, arg);
                    }
                }
                
                PushFrame(func.Value.token.Span.ToString(), parameters);
                // Bring all record properties into scope
                RecordValue? oldRecord = currentRecord;

                if (func.Caller != null) {
                    currentRecord = func.Caller;
                    foreach (var (id, innerValue) in func.Caller.Properties) {
                        DeclareBinding(id, innerValue);
                    }
                }

                Value value = NilValue.The;

                try {
                    _ = Visit(func.Value.body);
                } catch (ReturnEx ret) {
                    value = ret.value;
                }

                PopFrame();
                PopScope();
                currentRecord = oldRecord;

                return value;
            }

            case RecordValue record: {
                if (record.Base?.Parameters != null) {
                    if (record.Base?.Parameters?.Length != node.arguments.Length) {
                        throw new BluException($"Record constructor received {node.arguments.Length} arguments, but expected {record.Base?.Parameters?.Length}");
                    }

                    PushScope();
                    Dictionary<string, Value> properties = new();

                    // Cloning the original properties
                    foreach (var (binding, value) in record.Properties) {
                        properties[binding] = value;
                    }

                    for (int i = 0; i < record.Base?.Parameters.Length; ++i) {
                        string binding = record.Base?.Parameters[i].Item1.token.Span.ToString();
                        properties[binding] = Visit(node.arguments[i]);
                    }

                    foreach (var prop in record.Base?.Inner) {
                        properties[prop.token.Span.ToString()] = Visit(prop);
                    }

                    PopScope();
                    RecordValue newRec = new(record.Base, properties);

                    foreach (var (_, value) in properties) {
                        if (value is FunctionValue f) {
                            f.Caller = newRec;
                        }
                    }

                    return newRec;
                } else {
                    return record.Clone();
                }
            }

            case NativeFunctionValue native: {
                if ((native.Func.Parameters?.Length ?? 0) != node.arguments.Length) {
                    throw new BluException($"Trying to call function '{native.Func.Identifier}' with {node.arguments.Length} arguments, but expected {native.Func.Parameters?.Length ?? 0}");
                }

                Value[] args = new Value[node.arguments.Length];
                (string, Value)[] parameters = new (string, Value)[node.arguments.Length];
                for (int i = 0; i < args.Length; ++i) {
                    Value value = Visit(node.arguments[i]);

                    parameters[i] = (native.Func.Parameters[i], value);
                    args[i] = value;
                }

                PushFrame(native.Func.Identifier, parameters);
                Value ret = native.Func.Func(this, args);
                PopFrame();

                return ret;
            }
        }

        throw new BluException($"Trying to call non-function value '{node.token.Span}':{lhs}");
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
        Value value = Visit(node.Expression);
        DeclareOrOverwriteBinding(node.token.Span.ToString(), value);
        return value;
    }

    Value VisitIdentifier(IdentifierNode node) => FindBinding(node.token.Span.ToString());

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

    Value VisitIndexGet(IndexGetNode node) {
        Value lhs = Visit(node.Lhs);
        Value index = Visit(node.Index);

        if (index is NumberValue number) {
            int numIndex = (int)number.Value;

            if (lhs is ListValue list) {
                if (numIndex < 0 || numIndex >= list.Values.Length) {
                    throw new BluException($"Index '{numIndex}' out of range of '{list.Values.Length}'");
                }
                return list.Values[numIndex];
            } else if (lhs is StringValue str) {
                if (numIndex < 0 || numIndex >= str.Value.Length) {
                    throw new BluException($"Index '{numIndex}' out of range of '{str.Value.Length}'");
                }
                return new CharValue(str.Value[numIndex]);
            }
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
            throw new BluException($"Record '{node.Lhs.token.Span}' does not contain property '{property}'");
        }

        throw new BluException($"Could not access non-record '{node.Lhs.token.Span}'");
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
        string binding = node.Lhs.token.Span.ToString();

        if (currentRecord != null && currentRecord.Properties.ContainsKey(binding)) {
            Value oldValue = currentRecord.Properties[binding];
            currentRecord.Properties[binding] = rhs;
            return oldValue;
        }

        return node.Lhs switch {
            IdentifierNode => SetBinding(binding, rhs),
            _ => throw new BluException($"Unsupported item in assignment '{node.Lhs}'"),
        };
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

        if (lhs.GetType() != rhs.GetType()) {
            return BoolValue.False;
        }

        return node.token.Kind switch {
            TokenKind.EqualEq => lhs.Equal(rhs),
            TokenKind.NotEqual => lhs.NotEqual(rhs),
            _ => throw new BluException("Invalid equality operator"),
        };
    }

    Value VisitComparison(ComparisonNode node) {
        Value lhs = Visit(node.Lhs);
        Value rhs = Visit(node.Rhs);

        if (lhs.GetType() != rhs.GetType()) {
            return BoolValue.False;
        }

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

    Value VisitObject(ObjectNode node) {
        PushScope();
        
        RecordValue? oldRecord = currentRecord;
        Dictionary<string, Value> inner = new();
        currentRecord = new(node, inner);
        
        if (node.Parameters != null) {
            for (int i = 0; i < node.Parameters.Length; ++i) {
                string binding = node.Parameters[i].Item1.token.Span.ToString();
                inner[binding] = NilValue.The;
                DeclareBinding(binding, NilValue.The);
            }
        }

        if (node.Composed != null) {
            foreach (var compose in node.Composed) {
                string itemName = compose.token.Span.ToString();
                Value composeItem = FindBinding(itemName);

                if (composeItem is RecordValue rec) {
                    foreach (var (key, value) in rec.Properties) {
                        inner[key] = value;
                        DeclareOrOverwriteBinding(key, value);
                    }
                } else {
                    throw new BluException($"Cannot compose with non-class item '{itemName}'");
                }
            }
        }

        foreach (var binding in node.Inner) {
            inner[binding.token.Span.ToString()] = VisitBinding(binding);
        }

        foreach (var (_, value) in inner) {
            if (value is FunctionValue func) {
                func.Caller = currentRecord;
            }
        }

        PopScope();
        RecordValue record = currentRecord;
        currentRecord = oldRecord;
        return record;
    }

    Value VisitClone(CloneNode node) {
        Value value = Visit(node.Expression);
        return value switch {
            RecordValue n => n.Clone(),
            _ => value,
        };
    }

    Value VisitEnvironmentOpen(EnvironmentOpenNode node) {
        Value value = Visit(node.Lhs);

        if (value is RecordValue record) {
            PushScope();

            foreach (var binding in record.Properties) {
                DeclareBinding(binding.Key, binding.Value);
            }

            VisitBody(node.Inner);

            PopScope();
            return record;
        }
        throw new BluException($"Cannot open non-record {value}");
    }
}