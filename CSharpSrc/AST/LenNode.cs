namespace Blu;

sealed class LenNode : AstNode {
    public readonly AstNode Expression;
    
    public LenNode(Token token, AstNode expression) : base(token) {
        this.Expression = expression;
    }
}