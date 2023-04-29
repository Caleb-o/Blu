namespace Blu;

sealed class ForLoopNode : AstNode {
    public readonly AstNode Start;
    public readonly AstNode To;
    public readonly AstNode Body;

    public ForLoopNode(Token token, AstNode start, AstNode to, AstNode body) : base(token) {
        this.Start = start;
        this.To = to;
        this.Body = body;
    }
}