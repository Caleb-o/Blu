namespace Blu;

sealed class PropertyGetNode : AstNode {
    public readonly AstNode Lhs;
    public readonly IdentifierNode Rhs;

    public PropertyGetNode(Token token, AstNode lhs, IdentifierNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}