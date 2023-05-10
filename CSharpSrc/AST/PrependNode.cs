namespace Blu;

sealed class PrependNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public PrependNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}