namespace Blu {
    // Types are the RHS value of a variable/parameter
    // Note: Mutability is not tied to the type
    sealed class TypeNode : AstNode
    {
        public string? typeName { get; private set; }
        public bool isReference { get; private set; }

        public TypeNode(Token token, bool isReference) : base(token)
        {
            this.typeName = token.lexeme;
            this.isReference = isReference;
        }
    }
}