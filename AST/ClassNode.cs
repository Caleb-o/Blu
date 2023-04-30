namespace Blu;

sealed class ClassNode : AstNode {
    public readonly BindingNode[] Inner;
    public readonly IdentifierNode[]? Composed;

    public ClassNode(Token token, BindingNode[] inner, IdentifierNode[]? composed) : base(token) {
        this.Inner = inner;
        this.Composed = composed;
    }
}