using System;
using System.Text;
using System.Collections.Generic;

namespace Blu.Runtime;

interface Value {
    object GetValue();

    Value Add(Value rhs);
    Value Sub(Value rhs);
    Value Mul(Value rhs);
    Value Div(Value rhs);

    BoolValue Less(Value rhs);
    BoolValue LessEq(Value rhs);
    BoolValue Greater(Value rhs);
    BoolValue GreaterEq(Value rhs);
    BoolValue Equal(Value rhs);
    BoolValue NotEqual(Value rhs);
}

sealed class NilValue : Value {
    public readonly static NilValue The = new();

    public override string ToString() => "nil";

    public object GetValue() => The;

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on nil");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on nil");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on nil");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on nil");

    public BoolValue Less(Value rhs) => throw new BluException("Cannot use operation on nil");
    public BoolValue LessEq(Value rhs) => throw new BluException("Cannot use operation on nil");
    public BoolValue Greater(Value rhs) => throw new BluException("Cannot use operation on nil");
    public BoolValue GreaterEq(Value rhs) => throw new BluException("Cannot use operation on nil");
    public BoolValue Equal(Value rhs) => throw new BluException("Cannot use operation on nil");
    public BoolValue NotEqual(Value rhs) => throw new BluException("Cannot use operation on nil");
}

sealed class NumberValue : Value {
    public readonly double Value;

    public NumberValue(double value) {
        Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => Value.ToString();

    public Value Add(Value rhs) => new NumberValue(Value + ((NumberValue)rhs).Value);
    public Value Sub(Value rhs) => new NumberValue(Value - ((NumberValue)rhs).Value);
    public Value Mul(Value rhs) => new NumberValue(Value * ((NumberValue)rhs).Value);
    public Value Div(Value rhs) => new NumberValue(Value / ((NumberValue)rhs).Value);

    public BoolValue Less(Value rhs) => new(Value < ((NumberValue)rhs).Value);
    public BoolValue LessEq(Value rhs) => new(Value <= ((NumberValue)rhs).Value);
    public BoolValue Greater(Value rhs) => new(Value > ((NumberValue)rhs).Value);
    public BoolValue GreaterEq(Value rhs) => new(Value >= ((NumberValue)rhs).Value);
    public BoolValue Equal(Value rhs) => new(Value == ((NumberValue)rhs).Value);
    public BoolValue NotEqual(Value rhs) => new(Value != ((NumberValue)rhs).Value);
}

sealed class BoolValue : Value {
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    public readonly bool Value;

    public BoolValue(bool value) {
        Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => Value.ToString();

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on bool");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on bool");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on bool");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on bool");

    public BoolValue Less(Value rhs) => throw new BluException("Cannot use operations on bool");
    public BoolValue LessEq(Value rhs) => throw new BluException("Cannot use operations on bool");
    public BoolValue Greater(Value rhs) => throw new BluException("Cannot use operations on bool");
    public BoolValue GreaterEq(Value rhs) => throw new BluException("Cannot use operations on bool");
    public BoolValue Equal(Value rhs) => new(Value == ((BoolValue)rhs).Value);
    public BoolValue NotEqual(Value rhs) => new(Value != ((BoolValue)rhs).Value);
}

sealed class CharValue : Value {
    public readonly char Value;

    public CharValue(char value) {
        Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => $"{Value}";

    public Value Add(Value rhs) => new StringValue($"{Value}{((CharValue)rhs).Value}");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on char");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on char");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on char");

    public BoolValue Less(Value rhs) => new((int)Value < (int)((CharValue)rhs).Value);
    public BoolValue LessEq(Value rhs) => new((int)Value <= (int)((CharValue)rhs).Value);
    public BoolValue Greater(Value rhs) => new((int)Value > (int)((CharValue)rhs).Value);
    public BoolValue GreaterEq(Value rhs) => new((int)Value >= (int)((CharValue)rhs).Value);
    public BoolValue Equal(Value rhs) => new(Value == ((CharValue)rhs).Value);
    public BoolValue NotEqual(Value rhs) => new(Value != ((CharValue)rhs).Value);
}

sealed class StringValue : Value {
    public readonly string Value;

    public StringValue(string value) {
        Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => Value;

    public Value Add(Value rhs) => new StringValue(Value + ((StringValue)rhs).Value);
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on string");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on string");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on string");

    public BoolValue Less(Value rhs) => new(Value.Length < ((StringValue)rhs).Value.Length);
    public BoolValue LessEq(Value rhs) => new(Value.Length <= ((StringValue)rhs).Value.Length);
    public BoolValue Greater(Value rhs) => new(Value.Length > ((StringValue)rhs).Value.Length);
    public BoolValue GreaterEq(Value rhs) => new(Value.Length >= ((StringValue)rhs).Value.Length);
    public BoolValue Equal(Value rhs) => new(Value == ((StringValue)rhs).Value);
    public BoolValue NotEqual(Value rhs) => new(Value != ((StringValue)rhs).Value);
}

sealed class FunctionValue : Value {
    public readonly FunctionNode Value;
    public RecordValue? Caller;

    public FunctionValue(FunctionNode value) {
        Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => "func";

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on functions");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on functions");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on functions");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on functions");

    public BoolValue Less(Value rhs) => throw new BluException("Cannot use operation on function");
    public BoolValue LessEq(Value rhs) => throw new BluException("Cannot use operation on function");
    public BoolValue Greater(Value rhs) => throw new BluException("Cannot use operation on function");
    public BoolValue GreaterEq(Value rhs) => throw new BluException("Cannot use operation on function");
    public BoolValue Equal(Value rhs) => throw new BluException("Cannot use operation on function");
    public BoolValue NotEqual(Value rhs) => throw new BluException("Cannot use operation on function");
}

sealed class NativeFunctionValue : Value {
    public readonly Internal.Function Func;
    public RecordValue? Caller;

    public NativeFunctionValue(Internal.Function func) {
        this.Func = func;
    }

    public object GetValue() => Func;

    public override string ToString() => $"nativefunc<func>";

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on native functions");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on native functions");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on native functions");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on native functions");

    public BoolValue Less(Value rhs) => throw new BluException("Cannot use operation on native function");
    public BoolValue LessEq(Value rhs) => throw new BluException("Cannot use operation on native function");
    public BoolValue Greater(Value rhs) => throw new BluException("Cannot use operation on native function");
    public BoolValue GreaterEq(Value rhs) => throw new BluException("Cannot use operation on native function");
    public BoolValue Equal(Value rhs) => throw new BluException("Cannot use operation on native function");
    public BoolValue NotEqual(Value rhs) => throw new BluException("Cannot use operation on native function");
}

sealed class ListValue : Value {
    public readonly Value[] Values;

    public ListValue(Value[] values) {
        Values = values;
    }

    public object GetValue() => Values;

    public override string ToString() {
        StringBuilder sb = new();

        sb.Append('[');
        for (int i = 0; i < Values.Length; ++i) {
            sb.Append(Values[i]);

            if (i < Values.Length - 1) {
                sb.Append(", ");
            }
        }
        sb.Append(']');

        return sb.ToString();
    }

    public Value Prepend(Value lhs) {
        Value[] values = new Value[Values.Length + 1];
        values[0] = lhs;
        Array.Copy(Values, 0, values, 1, Values.Length);
        return new ListValue(values);
    }

    public Value Add(Value rhs) { 
        Value[] values = new Value[Values.Length + ((ListValue)rhs).Values.Length];
        Array.Copy(Values, values, Values.Length);
        Array.Copy(((ListValue)rhs).Values, 0, values, Values.Length, ((ListValue)rhs).Values.Length);

        return new ListValue(values);
    }

    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on functions");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on functions");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on functions");

    public BoolValue Less(Value rhs) => new(Values.Length < ((ListValue)rhs).Values.Length);
    public BoolValue LessEq(Value rhs) => new(Values.Length <= ((ListValue)rhs).Values.Length);
    public BoolValue Greater(Value rhs) => new(Values.Length > ((ListValue)rhs).Values.Length);
    public BoolValue GreaterEq(Value rhs) => new(Values.Length >= ((ListValue)rhs).Values.Length);
    public BoolValue Equal(Value rhs) => new(Values == ((ListValue)rhs).Values);
    public BoolValue NotEqual(Value rhs) => new(Values != ((ListValue)rhs).Values);
}

sealed class RecordValue : Value {
    public RecordValue? Parent { get; private set; }
    public readonly ObjectNode? Base;
    public readonly Dictionary<string, Value> Properties;

    public RecordValue(ObjectNode? b, Dictionary<string, Value> properties) {
        this.Base = b;
        this.Properties = properties;
    }

    public object GetValue() => Properties;

    public void SetParent(RecordValue? parent) => Parent = parent;

    public bool TryGetValue(string identifier, out Value value) {
        if (Properties.TryGetValue(identifier, out value)) {
            return true;
        }

        return Parent?.TryGetValue(identifier, out value) ?? false;
    }

    public RecordValue Clone() =>
        new(Base, new Dictionary<string, Value>(Properties));

    public override string ToString() {
        StringBuilder sb = new();

        sb.Append('{');
        int i = 0;
        foreach (var item in Properties) {
            sb.Append($"{item.Key}: {item.Value}");

            if (i++ < Properties.Count - 1) {
                sb.Append(", ");
            }
        }
        sb.Append('}');

        return sb.ToString();
    }

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on records");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on records");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on records");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on records");

    public BoolValue Less(Value rhs) => new(Properties.Count < ((RecordValue)rhs).Properties.Count);
    public BoolValue LessEq(Value rhs) => new(Properties.Count <= ((RecordValue)rhs).Properties.Count);
    public BoolValue Greater(Value rhs) => new(Properties.Count > ((RecordValue)rhs).Properties.Count);
    public BoolValue GreaterEq(Value rhs) => new(Properties.Count >= ((RecordValue)rhs).Properties.Count);
    public BoolValue Equal(Value rhs) => new(Properties == ((RecordValue)rhs).Properties);
    public BoolValue NotEqual(Value rhs) => new(Properties != ((RecordValue)rhs).Properties);
}

sealed class NativeValue : Value {

    public readonly object Value;

    public NativeValue(object value) {
        this.Value = value;
    }

    public object GetValue() => Value;

    public override string ToString() => $"native<Value>";

    public Value Add(Value rhs) => throw new BluException("Cannot use operations on NativeValue");
    public Value Sub(Value rhs) => throw new BluException("Cannot use operations on NativeValue");
    public Value Mul(Value rhs) => throw new BluException("Cannot use operations on NativeValue");
    public Value Div(Value rhs) => throw new BluException("Cannot use operations on NativeValue");

    public BoolValue Less(Value rhs) => throw new BluException("Cannot use operation on NativeValue");
    public BoolValue LessEq(Value rhs) => throw new BluException("Cannot use operation on NativeValue");
    public BoolValue Greater(Value rhs) => throw new BluException("Cannot use operation on NativeValue");
    public BoolValue GreaterEq(Value rhs) => throw new BluException("Cannot use operation on NativeValue");

    // TODO
    public BoolValue Equal(Value rhs) => throw new BluException("Cannot use operation on NativeValue");
    public BoolValue NotEqual(Value rhs) => throw new BluException("Cannot use operation on NativeValue");
}