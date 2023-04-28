using System.Collections.Generic;

namespace Blu;

sealed class BodyNode : AstNode
{
    public readonly List<AstNode> statements = new();
    
    public BodyNode() : base(null)
    {
    }

    public void AddNode(AstNode node) {
        this.statements.Add(node);
    }
}