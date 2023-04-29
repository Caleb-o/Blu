using System;
using System.Collections.Generic;

namespace Blu.Runtime;

interface Value {
    Value Add(Value rhs);
    Value Sub(Value rhs);
    Value Mul(Value rhs);
    Value Div(Value rhs);
}

sealed class NilValue : Value {
    public readonly static NilValue The = new();

    public override string ToString() => "nil";

    public Value Add(Value rhs) { throw new BluException("Cannot use operations on nil"); }
    public Value Sub(Value rhs) { throw new BluException("Cannot use operations on nil"); }
    public Value Mul(Value rhs) { throw new BluException("Cannot use operations on nil"); }
    public Value Div(Value rhs) { throw new BluException("Cannot use operations on nil"); }
}

sealed class NumberValue : Value {
    public readonly double Value;

    public NumberValue(double value) {
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public Value Add(Value rhs) => new NumberValue(Value + ((NumberValue)rhs).Value);
    public Value Sub(Value rhs) => new NumberValue(Value - ((NumberValue)rhs).Value);
    public Value Mul(Value rhs) => new NumberValue(Value * ((NumberValue)rhs).Value);
    public Value Div(Value rhs) => new NumberValue(Value / ((NumberValue)rhs).Value);
}

sealed class BoolValue : Value {
    public readonly bool Value;

    public BoolValue(bool value) {
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public Value Add(Value rhs) { throw new BluException("Cannot use operations on bool"); }
    public Value Sub(Value rhs) { throw new BluException("Cannot use operations on bool"); }
    public Value Mul(Value rhs) { throw new BluException("Cannot use operations on bool"); }
    public Value Div(Value rhs) { throw new BluException("Cannot use operations on bool"); }
}

sealed class StringValue : Value {
    public readonly string Value;

    public StringValue(string value) {
        Value = value;
    }

    public override string ToString() => Value;

    public Value Add(Value rhs) => new StringValue(Value + ((StringValue)rhs).Value);
    public Value Sub(Value rhs) { throw new BluException("Cannot use operations on string"); }
    public Value Mul(Value rhs) { throw new BluException("Cannot use operations on string"); }
    public Value Div(Value rhs) { throw new BluException("Cannot use operations on string"); }
}

sealed class FunctionValue : Value {
    public readonly FunctionNode Value;

    public FunctionValue(FunctionNode value) {
        Value = value;
    }

    public override string ToString() => "func";

    public Value Add(Value rhs) { throw new BluException("Cannot use operations on functions"); }
    public Value Sub(Value rhs) { throw new BluException("Cannot use operations on functions"); }
    public Value Mul(Value rhs) { throw new BluException("Cannot use operations on functions"); }
    public Value Div(Value rhs) { throw new BluException("Cannot use operations on functions"); }
}

sealed class ListValue : Value {
    public readonly Value[] Values;

    public ListValue(Value[] values) {
        Values = values;
    }

    public override string ToString() => "list";

    public Value Add(Value rhs) { 
        Value[] values = new Value[Values.Length + ((ListValue)rhs).Values.Length];
        Array.Copy(Values, values, Values.Length);
        Array.Copy(((ListValue)rhs).Values, 0, values, Values.Length, ((ListValue)rhs).Values.Length);

        return new ListValue(values);
    }
    public Value Sub(Value rhs) { throw new BluException("Cannot use operations on functions"); }
    public Value Mul(Value rhs) { throw new BluException("Cannot use operations on functions"); }
    public Value Div(Value rhs) { throw new BluException("Cannot use operations on functions"); }
}