namespace Blu;

sealed class AndNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public AndNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}