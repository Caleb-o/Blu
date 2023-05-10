using System.Collections.Generic;

namespace Blu;

sealed class PrintNode : AstNode {
    public readonly AstNode[] Arguments;

    public PrintNode(Token token, AstNode[] arguments) : base(token) {
        this.Arguments = arguments;
    }
}