namespace Blu {
    sealed class LiteralNode : AstNode
    {
        public LiteralNode(Token? token) : base(token)
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