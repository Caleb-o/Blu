namespace Blu;

enum BindingKind {
    None, Mutable, Recursive,
}

sealed class BindingNode : AstNode {
    public readonly bool Explicit;
    public readonly BindingKind Kind;
    public readonly AstNode Expression;

    public BindingNode(Token token, bool exp, BindingKind Kind, AstNode expression) : base(token) {
        this.Explicit = exp;
        this.Kind = Kind;
        this.Expression = expression;
    }
}