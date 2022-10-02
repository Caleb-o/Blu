namespace Blu {
    // This is only used where Nodes are required to be returned, but we need to error
    sealed class ErrorNode : AstNode
    {
        public ErrorNode() : base(null)
        {
        }

        public override string ToCSharpString()
        {
            throw new NotImplementedException();
        }

        public override string ToLispyString()
        {
            throw new NotImplementedException();
        }
    }
}