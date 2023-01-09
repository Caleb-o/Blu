namespace Blu {
    sealed class CastNode : AstNode
    {
        public readonly AstNode expression;

        public CastNode(Token token, AstNode expression) : base(token)
        {
            this.expression = expression;
        }
    }
}