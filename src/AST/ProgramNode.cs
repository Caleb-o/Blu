using System.Text;

namespace Blu {
    sealed class ProgramNode : AstNode
    {
        public BodyNode? body { get; private set; }

        public ProgramNode() : base(null)
        {
            this.body = new BodyNode();
        }

        public override string ToCSharpString() {
            StringBuilder sb = new StringBuilder();

            return sb.ToString();
        }

        public override string ToLispyString() => $"(Program {body?.ToLispyString()})";
    }
}