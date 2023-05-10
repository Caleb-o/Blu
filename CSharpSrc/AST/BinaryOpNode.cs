using System.Text;

namespace Blu;

sealed class BinaryOpNode : AstNode
{
    public readonly AstNode lhs;
    public readonly AstNode rhs;

    public BinaryOpNode(Token token, AstNode lhs, AstNode rhs) : base(token) {
        this.lhs = lhs;
        this.rhs = rhs;
    }
}