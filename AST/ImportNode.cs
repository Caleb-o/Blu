namespace Blu;

sealed class ImportNode : AstNode {
    public readonly bool FromBase;
    public readonly IdentifierNode[] Path;

    public ImportNode(Token token, bool fromBase, IdentifierNode[] path) : base(token) {
        this.FromBase = fromBase;
        this.Path = path;
    }
}