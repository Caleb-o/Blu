namespace Blu;

sealed class BindingNode : AstNode {
    public readonly AstNode expression;

    public BindingNode(Token token, AstNode expression) : base(token) {
        this.expression = expression;
    }
}