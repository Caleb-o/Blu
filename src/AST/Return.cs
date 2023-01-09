
namespace Blu {
    sealed class Return : AstNode
    {
        public readonly AstNode rhs;

        public Return(Token token, AstNode rhs) : base(token)
        {
            this.rhs = rhs;
        }
    }
}