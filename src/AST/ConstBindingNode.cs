using System.Text;
using System.Diagnostics;

namespace Blu {
    sealed class ConstBindingNode : AstNode
    {
        public bool isPublic { get; private set; }
        public TypeNode? type { get; private set; }
        public AstNode? expression { get; private set; }
        public bool useInference { get; private set; }

        // Binding was provided with a type
        public ConstBindingNode(Token token, bool isPublic, TypeNode type, AstNode? expression) : base(token)
        {
            this.isPublic = isPublic;
            this.type = type;
            this.expression = expression;
            this.useInference = false;
        }

        // Binding will use inference
        // We can use inference in the binding since it should be constant
        // The C# code will require a type, which we will know
        // Note: We guarantee that the expression is of a single type, an expression is required
        public ConstBindingNode(Token token, bool isPublic, AstNode expression) : base(token)
        {
            this.isPublic = isPublic;
            this.expression = expression;
            this.useInference = true;
        }

        // Set the type of the constant binding
        // This should ONLY be used when inferring the type, as inferring the type again may cause issues
        public void SetType(TypeNode type) {
            Debug.Assert(!useInference, "Cannot set type if a type was already provided");
            this.type = type;
        }
    }
}