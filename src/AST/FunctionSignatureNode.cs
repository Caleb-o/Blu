namespace Blu {
    sealed class FunctionSignatureNode : AstNode
    {
        public readonly ParameterNode[] parameters;
        public readonly TypeNode returnType;

        public FunctionSignatureNode(Token token, ParameterNode[] parameters, TypeNode returnType) : base(token)
        {
            this.parameters = parameters;
            this.returnType = returnType;
        }
    }
}