namespace Blu {
    sealed class IdentifierNode : AstNode
    {
        public IdentifierNode(Token? token) : base(token)
        {
            this.isExpression = true;
        }
    }
}