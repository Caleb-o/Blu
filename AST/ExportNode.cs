namespace Blu;

sealed class ExportNode : AstNode {
    public readonly IdentifierNode[] Identifiers;

    public ExportNode(Token token, IdentifierNode[] identifiers) : base(token) {
        this.Identifiers = identifiers;
    }
}