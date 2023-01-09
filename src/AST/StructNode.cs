namespace Blu {
    sealed class StructNode : AstNode
    {
        public readonly bool isPublic;
        public readonly bool isRef;
        public readonly Token[] implements;
        public readonly StructField[] fields;

        public StructNode(Token token, bool isPublic, bool isRef, Token[] implements, StructField[] fields) : base(token)
        {
            this.isPublic = isPublic;
            this.isRef = isRef;
            this.implements = implements;
            this.fields = fields;
        }
    }
}