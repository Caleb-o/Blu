using System.Text;

namespace Blu {
    sealed class FunctionNode : AstNode
    {
        public bool isPublic { get; private set; }
        public FunctionSignatureNode signature { get; private set; }
        public BodyNode body { get; private set; }

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