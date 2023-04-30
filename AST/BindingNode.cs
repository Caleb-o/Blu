namespace Blu;

enum BindingKind {
    None, Mutable, Recursive,
}

sealed class BindingNode : AstNode {
    public readonly bool Explicit;
    public readonly BindingKind Kind;
    public readonly AstNode expression;

    public BindingNode(Token token, bool exp, BindingKind Kind, AstNode expression) : base(token) {
        this.Explicit = exp;
        this.Kind = Kind;
        this.expression = expression;
    }
}