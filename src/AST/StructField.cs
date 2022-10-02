namespace Blu {
    sealed class StructField : AstNode
    {
        public TypeNode? type { get; private set; }

        public StructField(Token token, TypeNode type) : base(token)
        {
            this.type = type;
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