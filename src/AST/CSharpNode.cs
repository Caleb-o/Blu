using System.Text;

namespace Blu {
    sealed class CSharpNode : AstNode
    {
        public CSharpNode(Token token) : base(token)
        {
        }

        public override string ToCSharpString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(token.lexeme);
            return sb.ToString();
        }

        public override string ToLispyString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(CSharp ");
            sb.Append(token.lexeme);
            sb.Append(')');
            return sb.ToString();
        }
    }
}