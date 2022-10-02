namespace Blu {
    sealed class LiteralNode : AstNode
    {
        public LiteralNode(Token? token) : base(token)
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