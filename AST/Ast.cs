namespace Blu;

abstract class AstNode {
    public readonly Token token;
    public AstNode(Token token) {
        this.token = token;
    }
}