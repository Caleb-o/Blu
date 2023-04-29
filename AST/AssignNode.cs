namespace Blu;

sealed class AssignNode : AstNode {
    public readonly AstNode Lhs;
    public readonly AstNode Expression;

    public AssignNode(Token token, AstNode lhs, AstNode expression) : base(token) {
        this.Lhs = lhs;
        this.Expression = expression;
    }
}