namespace Blu.Analysis;

sealed class BindingSymbol {
    public readonly Span Identifier;
    // Builtins will not have tokens
    public readonly Token Token;

    public readonly bool Final;
    public readonly bool Mutable;
    public BindingSymbol(Token token, Span identifier, bool final, bool mutable) {
        this.Token = token;
        this.Identifier = identifier;
        this.Final = final;
        this.Mutable = mutable;
    }
}