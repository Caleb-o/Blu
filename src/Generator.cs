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
            File.WriteAllText("experimenting.txt", sb.ToString());
        }

        string GetPadding() {
            return new string(' ', depth * 4);
        }

        void Push() {
            depth++;
        }

        void Pop() {
            depth--;
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
                
                case CSharpNode c:
                    VisitCSharp(c);
                    break;
                
                case IdentifierNode i:
                    sb.Append(i.token?.lexeme);
                    break;
                
                case StructNode s:
                    VisitStruct(s);
                    break;

                case TraitNode t:
                    VisitTrait(t);
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
            Push();

            foreach (AstNode n in node.statements) {
                Visit(n);
            }

            Pop();
            AppendLine("}");
        }

        void VisitFunctionSignature(FunctionSignatureNode node, bool isEntry) {
            sb.Append($"{node.returnType.GetTypeName()} ");
            sb.Append((isEntry) ? "Main" : node.token?.lexeme);
            sb.Append('(');

            int i = 0;
            foreach (var param in node.parameters) {
                sb.Append($"{param.type.typeName} {param.token?.lexeme}");

                if (i++ < node.parameters.Length - 1) {
                    sb.Append(", ");
                }
            }

            sb.Append(')');
        }

        void VisitFunctionNode(FunctionNode node) {
            sb.Append(GetPadding());
            sb.Append((node.isPublic || node.isEntry) ? "public " : "private ");
            sb.Append((node.isEntry) ? "static " : "");
            VisitFunctionSignature(node.signature, node.isEntry);
            sb.Append(' ');

            VisitBodyNode(node.body);
            sb.AppendLine();
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
            AppendLine(node.token.ToString());
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
            switch (node.token?.kind) {
                case TokenKind.String:
                    sb.Append($"\"{node.token}\"");
                    break;
                
                case TokenKind.Float:
                    sb.Append($"{node.token}f");
                    break;
                
                default:
                    sb.Append(node.token);
                    break;
            }
        }

        void VisitIdentifier(IdentifierNode node) {
            sb.Append(node.token);
        }

        void VisitStruct(StructNode node) {
            string vis = (node.isPublic) ? "public" : "private";
            if (node.isRef) {
                AppendLine($"{vis} class {node.token?.lexeme} {{");
            } else {
                AppendLine($"{vis} struct {node.token?.lexeme} {{");
            }

            // FIXME: Add inheritance/implements identifiers here

            Push();

            string fields = "";
            int i = 0;
            foreach (var field in node.fields) {
                VisitStructField(field);

                fields += $"{field.type?.typeName} {field.token?.lexeme}";

                if (i++ < node.fields.Length - 1) {
                    fields += ", ";
                }
            }

            sb.AppendLine();

            // Generate default constructor
            DefaultStructConstructor(node, fields);

            sb.AppendLine();

            // Generate default ToString method
            DefaultStructToString(node);

            Pop();
            AppendLine("}\n");
        }

        void VisitTrait(TraitNode node) {
            string vis = (node.isPublic) ? "public" : "private";
            AppendLine($"{vis} interface {node.token} {{");

            Push();

            foreach (var sig in node.signatures) {
                sb.Append(GetPadding());
                VisitFunctionSignature(sig, false);
                sb.AppendLine(";");
            }

            Pop();

            AppendLine("}");
            sb.AppendLine();
        }

        void DefaultStructConstructor(StructNode node, string fields) {
            AppendLine($"public {node.token?.lexeme}({fields}) {{");

            Push();

            foreach (var field in node.fields) {
                AppendLine($"this.{field.token?.lexeme} = {field.token?.lexeme};");
            }

            Pop();

            AppendLine("}");
        }

        void DefaultStructToString(StructNode node) {
            AppendLine($"public override string ToString() {{");

            Push();

            sb.Append(GetPadding());
            sb.Append($"return $\"{node.token.lexeme} {{{{ ");

            int i = 0;
            foreach (var field in node.fields) {
                sb.Append($"{{{field.token}}}");

                if (i++ < node.fields.Length - 1) {
                    sb.Append(", ");
                }
            }

            sb.AppendLine(" }}\";");

            Pop();

            AppendLine("}");
        }

        void VisitStructField(StructField node) {
            AppendLine($"public {node.type.typeName} {node.token.lexeme};");
        }
    }
}