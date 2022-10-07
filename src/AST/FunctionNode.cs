using System.Text;

namespace Blu {
    sealed class FunctionNode : AstNode
    {
        public bool isPublic { get; private set; }
        public ParameterNode[] parameters { get; private set; }
        public TypeNode returnType { get; private set; }
        public BodyNode body { get; private set; }

        public FunctionNode(Token token, bool isPublic, ParameterNode[] parameters, TypeNode returnType, BodyNode body) : base(token)
        {
            this.isPublic = isPublic;
            this.parameters = parameters;
            this.returnType = returnType;
            this.body = body;
        }

        public override string ToCSharpString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append((isPublic) ? "public " : "private ");
            sb.Append(token?.lexeme);
            sb.Append('(');

            int i = 0;
            foreach (var param in parameters) {
                sb.Append($"{param.type.typeName} {param.token?.lexeme}");

                if (i++ < parameters.Length - 1) {
                    sb.Append(", ");
                }
            }

            sb.Append(')');

            sb.Append(body.ToCSharpString());

            return sb.ToString();
        }

        public override string ToLispyString()
        {
            throw new NotImplementedException();
        }
    }
}