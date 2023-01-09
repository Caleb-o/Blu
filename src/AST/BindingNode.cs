namespace Blu {
    sealed class BindingNode : AstNode
    {
        public readonly TypeNode type;
        public readonly AstNode expression;
        public readonly bool isMutable;
        public readonly bool useInference;

        // Binding was provided with a type
        public BindingNode(Token token, bool isMutable, TypeNode type, AstNode expression) : base(token)
        {
            this.type = type;
            this.expression = expression;
            this.isMutable = isMutable;
            this.useInference = false;
        }

        // Binding will use inference
        // Since we evaluate a type, either by specifying the type or inferring it, we
        // will always generate the variable with a type, instead of using var.
        public BindingNode(Token token, bool isMutable, AstNode expression) : base(token)
        {
            this.expression = expression;
            this.isMutable = isMutable;
            this.useInference = true;
        }
    }
}