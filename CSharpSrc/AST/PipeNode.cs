namespace Blu;

sealed class PipeNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Rhs;

    public PipeNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.Lhs = lhs;
        this.Rhs = rhs;
    }
}