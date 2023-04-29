namespace Blu;

sealed class EqualityNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public EqualityNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}