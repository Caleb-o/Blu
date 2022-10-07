using System.Text;

namespace Blu {
    sealed class ProgramNode : AstNode
    {
        public BodyNode? body { get; private set; }

        public ProgramNode() : base(null)
        {
            this.body = new BodyNode();
        }
    }
}