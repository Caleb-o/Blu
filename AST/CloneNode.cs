namespace Blu;

sealed class CloneNode : AstNode {
    public readonly AstNode Expression;

    public CloneNode(Token token, AstNode expression) : base(token) {
        this.Expression = expression;
    }
}