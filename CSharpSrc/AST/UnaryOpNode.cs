namespace Blu;

sealed class UnaryOpNode : AstNode {
    public readonly AstNode rhs;

    public UnaryOpNode(Token token, AstNode rhs) : base(token) {
        this.rhs = rhs;
    }
}