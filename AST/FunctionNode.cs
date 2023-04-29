using System.Collections.Generic;

namespace Blu;

sealed class FunctionNode : AstNode {
    public readonly IdentifierNode[] parameters;
    public readonly AstNode body;

    public FunctionNode(Token token, IdentifierNode[] parameters, AstNode body) : base(token) {
        this.parameters = parameters;
        this.body = body;
    }
}