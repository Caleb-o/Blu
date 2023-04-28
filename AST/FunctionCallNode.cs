using System.Collections.Generic;

namespace Blu;

sealed class FunctionCallNode : AstNode {
    public readonly AstNode lhs;
    public readonly List<AstNode> arguments;

    public FunctionCallNode(Token token, AstNode lhs, List<AstNode> arguments) : base(token) {
        this.lhs = lhs;
        this.arguments = arguments;
    }
}