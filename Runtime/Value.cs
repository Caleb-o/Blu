using System;
using System.Collections.Generic;

namespace Blu.Runtime;

interface Value {
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

sealed class StringValue : Value {
    public readonly string Value;

    public StringValue(string value) {
        Value = value;
    }

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

    public FunctionValue(FunctionNode value) {
        Value = value;
    }

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