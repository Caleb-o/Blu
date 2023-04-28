using System.Collections.Generic;

namespace Blu;

sealed class ExportNode : AstNode {
    public readonly List<IdentifierNode> identifiers;

    public ExportNode(Token token, List<IdentifierNode> identifiers) : base(token) {
        this.identifiers = identifiers;
    }
}