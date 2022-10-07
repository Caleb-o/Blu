using System.Text;

namespace Blu {
    sealed class BodyNode : AstNode
    {
        public List<AstNode> statements { get; private set; } = new List<AstNode>();
        
        public BodyNode() : base(null)
        {
        }

        public void AddNode(AstNode node) {
            this.statements.Add(node);
        }
    }
}