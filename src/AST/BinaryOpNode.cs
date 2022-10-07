using System.Text;

namespace Blu {
    sealed class BinaryOpNode : AstNode
    {
        public AstNode lhs { get; private set; }
        public AstNode rhs { get; private set; }

        public BinaryOpNode(Token token, AstNode lhs, AstNode rhs) : base(token)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }
    }
}