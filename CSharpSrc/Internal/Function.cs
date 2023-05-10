namespace Blu.Internal;

sealed class Function {
    public delegate Runtime.Value InternalFunc(Runtime.Interpreter interpreter, Runtime.Value[] args);

    public readonly string Identifier;
    public readonly string[]? Parameters;
    public readonly InternalFunc Func;

    public Function(string identifier, string[]? parameters, InternalFunc func) {
        this.Identifier = identifier;
        this.Parameters = parameters;
        this.Func = func;
    }
}