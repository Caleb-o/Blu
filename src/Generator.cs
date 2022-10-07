using System.Text;

namespace Blu {
    sealed class Generator {
        StringBuilder sb;
        int depth = 1;

        public Generator() {
            this.sb = new StringBuilder();
        }
        
        public void Generate(CompilationUnit unit) {
            VisitProgramNode(unit.ast);
            File.WriteAllText("experimenting.cs", sb.ToString());
        }

        string GetPadding() {
            return new string(' ', depth * 4);
        }

        void AppendLine(string str) {
            sb.AppendLine(GetPadding() + str);
        }

        void Visit(AstNode node) {
            switch (node) {
                case ProgramNode p:
                    VisitProgramNode(p);
                    break;
                
                case BodyNode b:
                    VisitBodyNode(b);
                    break;
                
                case FunctionNode f:
                    VisitFunctionNode(f);
                    break;

                case BinaryOpNode b:
                    VisitBinaryOp(b);
                    break;
                
                case UnaryOpNode u:
                    VisitUnaryOp(u);
                    break;
                
                case ConstBindingNode b:
                    VisitConstBinding(b);
                    break;
                
                case BindingNode b:
                    VisitBinding(b);
                    break;
                
                case LiteralNode l:
                    VisitLiteral(l);
                    break;

                default:
                    throw new UnreachableException($"Generator - {node}");
            }
        }

        void VisitProgramNode(ProgramNode node) {
            sb.AppendLine("namespace Application {");
            sb.Append("    sealed class Program ");
            VisitBodyNode(node.body);
            sb.AppendLine("}");
        }

        void VisitBodyNode(BodyNode node) {
            sb.AppendLine("{");
            depth++;

            foreach (AstNode n in node.statements) {
                Visit(n);
            }

            depth--;
            AppendLine("}");
        }

        void VisitFunctionNode(FunctionNode node) {
            sb.Append(GetPadding());
            sb.Append((node.isPublic) ? "public " : "private ");
            sb.Append($"{node.returnType.GetTypeName()} ");
            sb.Append(node.token?.lexeme);
            sb.Append('(');

            int i = 0;
            foreach (var param in node.parameters) {
                sb.Append($"{param.type.typeName} {param.token?.lexeme}");

                if (i++ < node.parameters.Length - 1) {
                    sb.Append(", ");
                }
            }

            sb.Append(") ");

            VisitBodyNode(node.body);
        }

        void VisitBinaryOp(BinaryOpNode node) {
            Visit(node.lhs);
            sb.Append($" {node.token} ");
            Visit(node.rhs);
        }

        void VisitUnaryOp(UnaryOpNode node) {
            sb.Append($"{node.token} ");
            Visit(node.rhs);
        }

        void VisitCSharp(CSharpNode node) {
            sb.Append(node.token);
        }

        void VisitConstBinding(ConstBindingNode node) {
            sb.Append($"{GetPadding()}const {node.GetTypeName()} {node.token} = ");
            Visit(node.expression);
            sb.AppendLine(";");
        }

        void VisitBinding(BindingNode node) {
            sb.Append($"{GetPadding()}{node.GetTypeName()} {node.token} = ");
            Visit(node.expression);
            sb.AppendLine(";");
        }

        void VisitLiteral(LiteralNode node) {
            if (node.token?.kind == TokenKind.String) {
                sb.Append($"\"{node.token}\"");
            } else {
                sb.Append(node.token);
            }
        }

        void VisitIdentifier(IdentifierNode node) {
            sb.Append(node.token);
        }
    }
}