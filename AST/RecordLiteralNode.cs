namespace Blu;

sealed class RecordLiteralNode : AstNode {
    public readonly (IdentifierNode, AstNode)[] Values;

    public RecordLiteralNode(Token token, (IdentifierNode, AstNode)[] values) : base(token) {
        this.Values = values;
    }
}