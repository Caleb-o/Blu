using System.IO;
using System.Text;

namespace Blu {
    sealed class Generator {
        StringBuilder sb;
        int depth = 0;

        public Generator() {
            this.sb = new StringBuilder();
        }
        
        public void Generate(CompilationUnit unit) {
            GenerateDefaultUsings();
            Push();
            VisitProgramNode(unit.ast);
            Pop();
            File.WriteAllText("out.blucs", sb.ToString());
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

        void GenerateDefaultUsings() {
            AppendLine("using System;");

            sb.AppendLine();
        }

        void Visit(AstNode node) {
            switch (node) {
                case ProgramNode n:         VisitProgramNode(n); break;
                case BodyNode n:            VisitBodyNode(n); break;
                case FunctionNode n:        VisitFunctionNode(n); break;
                case EnumAdt n:             VisitEnumAdt(n); break;
                case BinaryOpNode n:        VisitBinaryOp(n); break;
                case UnaryOpNode n:         VisitUnaryOp(n); break;
                case ConstBindingNode n:    VisitConstBinding(n); break;
                case BindingNode n:         VisitBinding(n); break;
                case LiteralNode n:         VisitLiteral(n); break;
                case CSharpNode n:          VisitCSharp(n); break;
                case IdentifierNode n:      sb.Append(n.token.lexeme); break;
                case StructNode n:          VisitStruct(n); break;
                case TraitNode n:           VisitTrait(n); break;
                case CastNode n:            VisitCast(n); break;
                case Return n:              VisitReturn(n); break;
                
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

        void VisitEnumAdt(EnumAdt node) {
            string enumId = node.token.lexeme;
            string visibility = node.IsPublic ? "public" : "private";
            sb.Append($"{GetPadding()}sealed {visibility} class {enumId} {{\n");
            Push();

            foreach (var (token, field) in node.Fields) {
                switch (field) {
                    case NumericField num: {
                        sb.AppendLine($"{GetPadding()}const int {token.lexeme} = {num.Value};");
                        break;
                    }

                    case TupleField tuple: {
                        sb.AppendLine($"{GetPadding()}public class {token.lexeme} {{");
                        Push();

                        for (int i = 0; i < tuple.Types.Length; ++i) {
                            var type = tuple.Types[i];
                            sb.AppendLine($"{GetPadding()}public readonly {type.typeName} Item_{i};");
                        }

                        sb.Append($"{GetPadding()}public {token.lexeme}(");
                        for (int i = 0; i < tuple.Types.Length; ++i) {
                            var type = tuple.Types[i];
                            sb.Append($"{type.typeName} item_{i}");

                            if (i < tuple.Types.Length - 1) {
                                sb.Append(", ");
                            }
                        }
                        sb.AppendLine(") {");
                        Push();

                        for (int i = 0; i < tuple.Types.Length; ++i) {
                            sb.AppendLine($"{GetPadding()}this.Item_{i} = item_{i};");
                        }

                        Pop();
                        sb.AppendLine($"{GetPadding()}}}");

                        Pop();
                        sb.AppendLine($"{GetPadding()}}}");
                        break;
                    }

                    default:
                        throw new UnreachableException("Generator - VisitEnumAdt");
                }
            }

            Pop();
            sb.AppendLine($"{GetPadding()}}}");
        }

        void VisitBinaryOp(BinaryOpNode node) {
            Visit(node.lhs);
            sb.Append($" {node.token} ");
            Visit(node.rhs);
        }

        void VisitUnaryOp(UnaryOpNode node) {
            sb.Append($"{node.token}");
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
        
        void VisitCast(CastNode node) {
            sb.Append($"({node.token.lexeme})(");
            Visit(node.expression);
            sb.Append(')');
        }

        void VisitReturn(Return node) {
            sb.Append("return");
            if (node.rhs != null) {
                sb.Append(' ');
                Visit(node.rhs);
            }
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