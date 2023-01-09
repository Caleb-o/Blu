namespace Blu {
    sealed class TraitNode : AstNode
    {
        public readonly bool isPublic;
        public readonly FunctionSignatureNode[] signatures;

        public TraitNode(Token token, bool isPublic, FunctionSignatureNode[] signatures) : base(token)
        {
            this.isPublic = isPublic;
            this.signatures = signatures;
        }
    }
}