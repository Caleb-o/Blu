namespace Blu {
    sealed class BindingNode : AstNode
    {
        public TypeNode? type { get; private set; }
        public AstNode? expression { get; private set; }
        public bool isMutable { get; private set; }
        public bool useInference { get; private set; }

        string typeName = string.Empty;

        // Binding was provided with a type
        public BindingNode(Token token, bool isMutable, TypeNode type, AstNode? expression) : base(token)
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

        public void SetTypeName(string typeName) {
            this.typeName = typeName;
        }

        public override string ToCSharpString()
        {
            return $"{typeName} {token?.lexeme} = {expression?.ToCSharpString()};";
        }

        public override string ToLispyString()
        {
            throw new NotImplementedException();
        }
    }
}