namespace Blu {
    sealed class StructField : AstNode
    {
        public TypeNode? type { get; private set; }

        public StructField(Token token, TypeNode type) : base(token)
        {
            this.type = type;
        }
    }
}