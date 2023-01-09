namespace Blu {
    sealed class StructField : AstNode
    {
        public readonly TypeNode type;

        public StructField(Token token, TypeNode type) : base(token)
        {
            this.type = type;
        }
    }
}