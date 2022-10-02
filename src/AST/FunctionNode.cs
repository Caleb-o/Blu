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
            throw new NotImplementedException();
        }

        public override string ToLispyString()
        {
            throw new NotImplementedException();
        }
    }
}