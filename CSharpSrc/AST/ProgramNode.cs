using System.Text;

namespace Blu;

sealed class ProgramNode : AstNode {
    public readonly BodyNode body;

    public ProgramNode() : base(null) {
        this.body = new BodyNode();
    }
}