namespace Blu {
    sealed class TraitNode : AstNode
    {
        public bool isPublic { get; private set; }
        public FunctionSignatureNode[] signatures { get; private set; }

        public TraitNode(Token? token, bool isPublic, FunctionSignatureNode[] signatures) : base(token)
        {
            this.isPublic = isPublic;
            this.signatures = signatures;
        }
    }
}