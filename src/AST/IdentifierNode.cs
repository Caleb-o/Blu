namespace Blu {
    sealed class IdentifierNode : AstNode
    {
        public IdentifierNode(Token? token) : base(token)
        {
            this.isExpression = true;
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