using System.Collections.Generic;

namespace Blu;

sealed class FunctionNode : AstNode {
    public readonly List<IdentifierNode> parameters;
    public readonly BodyNode body;

    public FunctionNode(Token token, List<IdentifierNode> parameters, BodyNode body) : base(token) {
        this.parameters = parameters;
        this.body = body;
    }
}