namespace Blu;

enum BindingKind {
    None, Mutable, Recursive,
}

sealed class BindingNode : AstNode {
    public readonly BindingKind Kind;
    public readonly AstNode expression;

    public BindingNode(Token token, BindingKind Kind, AstNode expression) : base(token) {
        this.Kind = Kind;
        this.expression = expression;
    }
}