namespace Blu {
    sealed class FunctionSignatureNode : AstNode
    {
        public ParameterNode[] parameters { get; private set; }
        public TypeNode returnType { get; private set; }

        public FunctionSignatureNode(Token? token, ParameterNode[] parameters, TypeNode returnType) : base(token)
        {
            this.parameters = parameters;
            this.returnType = returnType;
        }
    }
}