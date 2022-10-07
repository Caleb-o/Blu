namespace Blu {
    sealed class ParameterNode : AstNode
    {
        public TypeNode type { get; private set; }
        public bool isMutable { get; private set; }

        public ParameterNode(Token token, TypeNode type, bool isMutable) : base(token)
        {
            this.type = type;
            this.isMutable = isMutable;
        }
    }
}