namespace Blu;

sealed class EnvironmentOpenNode : AstNode {
    public readonly AstNode Lhs;
    public readonly BodyNode Inner;

    public EnvironmentOpenNode(Token token, AstNode lhs, BodyNode inner) : base(token) {
        this.Lhs = lhs;
        this.Inner = inner;
    }
}