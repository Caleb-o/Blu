namespace Blu;

sealed class ListLiteralNode : AstNode {
    public readonly AstNode[] Expressions;

    public ListLiteralNode(Token token, AstNode[] expressions) : base(token) {
        this.Expressions = expressions;
    }
}