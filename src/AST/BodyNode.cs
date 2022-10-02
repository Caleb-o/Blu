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

        public override string ToCSharpString() {
            throw new NotImplementedException();
        }

        public override string ToLispyString() {
            StringBuilder sb = new StringBuilder();

            sb.Append("(Block ");

            foreach (AstNode node in statements) {
                sb.Append(node.ToLispyString());
            }

            sb.Append(')');

            return sb.ToString();
        }
    }
}