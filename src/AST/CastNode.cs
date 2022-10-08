namespace Blu {
    sealed class CastNode : AstNode
    {
        public AstNode expression { get; private set; }

        public CastNode(Token? token, AstNode expression) : base(token)
        {
            this.expression = expression;
        }
    }
}