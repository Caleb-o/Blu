namespace Blu;

sealed class ObjectNode : AstNode {
    public readonly (IdentifierNode, bool)[]? Parameters;
    public readonly BindingNode[] Inner;
    public readonly IdentifierNode[]? Composed;

    public ObjectNode(Token token, (IdentifierNode, bool)[]? parameters, BindingNode[] inner, IdentifierNode[]? composed) : base(token) {
        this.Parameters = parameters;
        this.Inner = inner;
        this.Composed = composed;
    }
}