namespace Blu {
    abstract class AstNode {
        public Token? token { get; protected set; }
        public bool isExpression { get; protected set; }

        public AstNode(Token? token) {
            this.token = token;
        }

        // Used for testing and simple visualisation
        public abstract string ToLispyString();
        // Used for generating the final C# code
        public abstract string ToCSharpString();
    }
}