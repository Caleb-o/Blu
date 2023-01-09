namespace Blu {
    sealed class ParameterNode : AstNode
    {
        public readonly TypeNode type;
        public readonly bool isMutable;

        public ParameterNode(Token token, TypeNode type, bool isMutable) : base(token)
        {
            this.type = type;
            this.isMutable = isMutable;
        }
    }
}