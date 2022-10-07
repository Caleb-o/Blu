namespace Blu {
    sealed class BindingNode : AstNode
    {
        public TypeNode? type { get; private set; }
        public AstNode? expression { get; private set; }
        public bool isMutable { get; private set; }
        public bool useInference { get; private set; }

        // Binding was provided with a type
        public BindingNode(Token token, bool isMutable, TypeNode type, AstNode? expression) : base(token)
        {
            this.type = type;
            this.expression = expression;
            this.isMutable = isMutable;
            this.useInference = false;
        }

        // Binding will use inference
        // We can use the 'var' keyword in C# to infer the type for us
        // Note: We guarantee that the expression is of a single type, an expression is required
        public BindingNode(Token token, bool isMutable, AstNode expression) : base(token)
        {
            this.expression = expression;
            this.isMutable = isMutable;
            this.useInference = true;
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