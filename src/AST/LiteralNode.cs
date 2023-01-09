namespace Blu {
    sealed class LiteralNode : AstNode
    {
        public LiteralNode(Token token) : base(token)
        {
            this.isExpression = true;
        }
    }
}