namespace Blu;

enum BindingKind {
    None, Mutable, Recursive,
}

sealed class BindingNode : AstNode {
    public readonly bool Final;
    public readonly BindingKind Kind;
    public readonly AstNode Expression;

    public BindingNode(Token token, bool final, BindingKind Kind, AstNode expression) : base(token) {
        this.Final = final;
        this.Kind = Kind;
        this.Expression = expression;
    }
}