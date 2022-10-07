using System.Text;

namespace Blu {
    sealed class UnaryOpNode : AstNode
    {
        public AstNode rhs { get; private set; }

        public UnaryOpNode(Token token, AstNode rhs) : base(token)
        {
            this.rhs = rhs;
        }
    }
}