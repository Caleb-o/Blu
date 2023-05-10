namespace Blu;

enum ImportKind {
    Normal, Base, Std,
}

sealed class ImportNode : AstNode {

    
    public readonly ImportKind Kind;
    public readonly IdentifierNode[] Path;

    public ImportNode(Token token, ImportKind kind, IdentifierNode[] path) : base(token) {
        this.Kind = kind;
        this.Path = path;
    }
}