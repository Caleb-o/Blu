namespace Blu;

sealed class ReturnNode : AstNode {
    public readonly AstNode? rhs;

    public ReturnNode(Token token, AstNode? rhs) : base(token) {
        this.rhs = rhs;
    }
}