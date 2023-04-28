using System.Collections.Generic;

namespace Blu;

sealed class FunctionNode : AstNode {
    public readonly IdentifierNode[] parameters;
    public readonly BodyNode body;

    public FunctionNode(Token token, IdentifierNode[] parameters, BodyNode body) : base(token) {
        this.parameters = parameters;
        this.body = body;
    }
}