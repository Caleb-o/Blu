namespace Blu {
    sealed class UnaryOpNode : AstNode
    {
        public AstNode rhs { get; private set; }

        public UnaryOpNode(Token token, AstNode rhs) : base(token)
        {
            this.rhs = rhs;
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