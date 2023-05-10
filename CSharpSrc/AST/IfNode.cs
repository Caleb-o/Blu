namespace Blu;

sealed class IfNode : AstNode {
    public readonly AstNode Condition;
    public readonly AstNode TrueBody;
    public readonly AstNode? FalseBody;

    public IfNode(Token token, AstNode condition, AstNode trueBody, AstNode? falseBody) : base(token) {
        this.Condition = condition;
        this.TrueBody = trueBody;
        this.FalseBody = falseBody;
    }
}