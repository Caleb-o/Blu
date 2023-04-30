namespace Blu;

sealed class ClassNode : AstNode {
    public readonly IdentifierNode[]? Parameters;
    public readonly BindingNode[] Inner;
    public readonly IdentifierNode[]? Composed;

    public ClassNode(Token token, IdentifierNode[]? parameters, BindingNode[] inner, IdentifierNode[]? composed) : base(token) {
        this.Parameters = parameters;
        this.Inner = inner;
        this.Composed = composed;
    }
}