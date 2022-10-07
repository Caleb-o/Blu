namespace Blu {
    sealed class StructNode : AstNode
    {
        public bool isPublic { get; private set; }
        public bool isRef { get; private set; }
        public Token[] implements { get; private set; }
        public StructField[] fields { get; private set; }

        public StructNode(Token? token, bool isPublic, bool isRef, Token[] implements, StructField[] fields) : base(token)
        {
            this.isPublic = isPublic;
            this.isRef = isRef;
            this.implements = implements;
            this.fields = fields;
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