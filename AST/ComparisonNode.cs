namespace Blu;

sealed class ComparisonNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public ComparisonNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}