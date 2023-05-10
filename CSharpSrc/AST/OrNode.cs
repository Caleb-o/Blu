namespace Blu;

sealed class OrNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public OrNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}