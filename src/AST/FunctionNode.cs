using System.Text;

namespace Blu {
    sealed class FunctionNode : AstNode
    {
        public readonly bool isPublic;
        public readonly FunctionSignatureNode signature;
        public readonly BodyNode body;

        public bool isEntry { get; private set; } = false;

        public FunctionNode(Token token, bool isPublic, FunctionSignatureNode signature, BodyNode body) : base(token)
        {
            this.isPublic = isPublic;
            this.signature = signature;
            this.body = body;
        }

        public void SetEntry() {
            this.isEntry = true;
        }
    }
}