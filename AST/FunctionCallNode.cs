using System.Collections.Generic;

namespace Blu;

sealed class FunctionCallNode : AstNode {
    public readonly AstNode lhs;
    public readonly AstNode[] arguments;

    public FunctionCallNode(Token token, AstNode lhs, AstNode[] arguments) : base(token) {
        this.lhs = lhs;
        this.arguments = arguments;
    }
}