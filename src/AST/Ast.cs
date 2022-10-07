namespace Blu {
    abstract class AstNode {
        public Token? token { get; protected set; }
        public bool isExpression { get; protected set; }
        
        protected string typeName = string.Empty;

        public AstNode(Token? token) {
            this.token = token;
        }

        public void SetTypeName(string typeName) {
            this.typeName = typeName;
        }

        public string GetTypeName() {
            return typeName;
        }
    }
}