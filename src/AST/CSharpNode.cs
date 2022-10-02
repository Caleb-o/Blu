using System.Text;

namespace Blu {
    sealed class CSharpNode : AstNode
    {
        public Token code { get; private set; }

        public CSharpNode(Token token, Token code) : base(token)
        {
            this.code = code;
        }

        public override string ToCSharpString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(code.lexeme);
            return sb.ToString();
        }

        public override string ToLispyString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(CSharp ");
            sb.Append(code.lexeme);
            sb.Append(')');
            return sb.ToString();
        }
    }
}