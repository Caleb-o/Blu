namespace Blu;

sealed class IndexGetNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Index;

    public IndexGetNode(Token token, AstNode lhs, AstNode index) : base(token) {
        this.Lhs = lhs;
        this.Index = index;
    }
}