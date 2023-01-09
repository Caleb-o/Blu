using System;
using System.Collections.Generic;
using System.Linq;

namespace Blu {
    sealed class Analyser {
        List<List<Symbol>> symbolTable = new();
        bool hadError = false;
        CompilationUnit unit;

        public Analyser(CompilationUnit unit) {
            this.unit = unit;

            PushScope();
            DefineBuiltinTypes();
        }

        public bool Analyse() {
            VisitProgram(this.unit.ast);
            return this.hadError;
        }

        void PushScope() {
            symbolTable.Add(new List<Symbol>());
        }

        void PopScope() {
            symbolTable.Remove(symbolTable.Last());
        }

        void SoftError(string message, Token token) {
            hadError = true;

            Console.WriteLine($"Error occured: {message} in {this.unit.fileName} at {token.line}:{token.column}");
        }

        // Note: This error will cause the compilation to stop entirely
        void Error(string message, Token token) {
            throw new ParserException(this.unit.fileName, message, (int)token.line, (int)token.column);
        }

        void TypeError(string identifier, Token token) {
            SoftError($"Type '{identifier}' does not exist in the current scope", token);
        }

        void DefineBuiltinTypes() {
            DeclareSymbol(new TypeSymbol("void"));
            DeclareSymbol(new TypeSymbol("int"));
            DeclareSymbol(new TypeSymbol("float"));
            DeclareSymbol(new TypeSymbol("bool"));
            DeclareSymbol(new TypeSymbol("string"));
        }

        TypeSymbol ErrorNoType(string identifier, Token token) {
            var typeErr = () => {
                TypeError(identifier, token);
                return (TypeSymbol)null;
            };

            return FindSymbol(identifier) switch {
                TraitSymbol t => new TypeSymbol(t.identifier, t.token),
                TypeSymbol t => t,
                _ => typeErr(),
            };
        }

        Symbol FindSymbol(string identifier) {
            for (int i = symbolTable.Count - 1; i >= 0; --i) {
                var table = symbolTable[i];

                for (int j = 0; j < table.Count; j++) {
                    if (table[j].identifier == identifier) {
                        return table[j];
                    }
                }
            }

            return null;
        }

        void DeclareSymbol(Symbol sym) {
            var found = FindSymbol(sym.identifier);
            if (found != null) {
                Error($"Item with name '{found.identifier}' at {sym.token.line}:{sym.token.column} already exists", found.token);
            }

            symbolTable[symbolTable.Count - 1].Add(sym);
        }

        void DefineSymbol(Symbol sym) {
            symbolTable[symbolTable.Count - 1].Add(sym);
        }

        BindingType GetBindingType(BindingNode node) {
            return (node.isMutable) ? BindingType.Var : BindingType.Let;
        }

        void Visit(AstNode node) {
            switch (node) {
                case ProgramNode n:         VisitProgram(n); break;
                case BodyNode n:            VisitBody(n, true); break;
                case StructNode n:          VisitStruct(n); break;
                case FunctionNode n:        VisitFunction(n); break;
                case EnumAdt n:             VisitEnumAdt(n); break;
                case BindingNode n:         VisitBinding(n); break;
                case ConstBindingNode n:    VisitConstBinding(n); break;
                case IdentifierNode n:      VisitIdentifier(n); break;
                case TraitNode n:           VisitTrait(n); break;
                case CastNode n:            VisitCast(n); break;
                case Return n:              VisitReturn(n); break;
                
                // Ignore
                case CSharpNode: break;
                
                default:
                    throw new UnreachableException($"Analyser - Visit ({node})");
            }
        }

        void VisitProgram(ProgramNode node) {
            VisitBody(node.body, false);
        }

        void VisitBody(BodyNode node, bool newScope) {
            if (newScope) PushScope();

            foreach (var n in node.statements) {
                Visit(n);
            }

            if (newScope) PopScope();
        }

        void VisitStruct(StructNode node) {
            // Defining now allows for recursive types
            DeclareSymbol(new TypeSymbol(node.token.lexeme, node.token));

            var fieldNames = new HashSet<string>();

            if (node.implements.Length > 0) {
                foreach (var impls in node.implements) {
                     if (fieldNames.Contains(impls.lexeme)) {
                        SoftError($"Struct '{node.token.lexeme}' has duplicate item in implements list named '{impls.lexeme}'", impls);
                    }

                    ErrorNoType(impls.lexeme, impls);
                    fieldNames.Add(impls.lexeme);
                }

                fieldNames.Clear();
            }

            foreach(var field in node.fields) {
                // Check for colliding names
                if (fieldNames.Contains(field.token.lexeme)) {
                    SoftError($"Struct '{node.token.lexeme}' has duplicate field named '{field.token.lexeme}'", field.token);
                }

                ErrorNoType(field.type.typeName, field.token);

                fieldNames.Add(field.token.lexeme);
            }
        }

        void VisitFunction(FunctionNode node) {
            List<TypeSymbol> parameterTypes = new();

            if (unit.isMainUnit && node.token.lexeme == "main") {
                node.SetEntry();
            }

            DeclareSymbol(new FunctionSymbol(node.token.lexeme, node.token, node.isPublic, parameterTypes, ErrorNoType(node.signature.returnType.token.lexeme, node.signature.token)));
            PushScope();

            VerifyFunctionSignature(node.signature, true, parameterTypes);

            VisitBody(node.body, false);

            PopScope();
        }

        void VisitEnumAdt(EnumAdt node) {
            HashSet<string> fields = new();

            foreach (var (field, kind) in node.Fields) {
                if (!fields.Add(field.lexeme)) {
                    SoftError($"Enum '{node.token.lexeme}' has an identical field '{field.lexeme}'", field);
                }

                if (kind is TupleField tuple) {
                    foreach (var type in tuple.Types) {
                        _ = ErrorNoType(type.token.lexeme, type.token);
                    }
                }
            }

            DeclareSymbol(new EnumSymbol(node.token.lexeme, fields, node.token));
        }

        void VisitBinding(BindingNode node) {
            TypeSymbol type = null;

            if (node.type == null) {
                type = InferTypeOfNode(node.expression);
            } else {
                type = (TypeSymbol)FindSymbol(node.type.token.lexeme);
                TypeSymbol errorType = ExpectTypeOfNode(node.expression, type);

                if (errorType != null) {
                    SoftError($"Identifier '{node.token.lexeme}' expected type '{type}' but received '{errorType}'", node.token);
                }
            }

            DeclareSymbol(new BindingSymbol(node.token.lexeme, node.token, false, GetBindingType(node), type));
            
            node.SetTypeName(type.identifier);
        }

        void VisitConstBinding(ConstBindingNode node) {
            TypeSymbol type = null;

            if (node.type == null) {
                type = InferTypeOfNode(node.expression);
            } else {
                type = (TypeSymbol)FindSymbol(node.type.token.lexeme);
                TypeSymbol errorType = ExpectTypeOfNode(node.expression, type);

                if (errorType != null) {
                    SoftError($"Identifier '{node.token.lexeme}' expected type '{type}' but received '{errorType}'", node.token);
                }
            }

            // FIXME: Check if expression can be evaluated at compile-time
            //        eg. Identifiers are constants and values are literals

            DeclareSymbol(new BindingSymbol(node.token.lexeme, node.token, node.isPublic, BindingType.Constant, type));

            node.SetTypeName(type.identifier);
        }

        void VisitIdentifier(IdentifierNode node) {
            var id = FindSymbol(node.token.lexeme);
            if (id == null) {
                SoftError($"Identifier '{node.token.lexeme}' does not exist", node.token);
            }
        }

        void VisitTrait(TraitNode node) {
            var trait = (TraitSymbol)FindSymbol(node.token.lexeme);
            if (trait != null) {
                SoftError($"Trait '{node.token.lexeme}' already exists at {trait.token.line}:{trait.token.column} already exists", node.token);
            }

            var functions = new List<FunctionSymbol>();
            var functionNames = new HashSet<string>();

            foreach (var sig in node.signatures) {
                if (functionNames.Contains(sig.token.lexeme)) {
                    SoftError($"Trait '{node.token}' has duplicate function named '{sig.token.lexeme}'", sig.token);
                }

                List<TypeSymbol> paramTypes = new List<TypeSymbol>();
                VerifyFunctionSignature(sig, false, paramTypes);

                functions.Add(new FunctionSymbol(sig.token.lexeme, sig.token, true, paramTypes, ErrorNoType(sig.returnType.token.lexeme, sig.token)));

                functionNames.Add(sig.token.lexeme);
            }

            DeclareSymbol(new TraitSymbol(node.token.lexeme, node.token, node.isPublic, functions));
        }

        void VisitCast(CastNode node) {
            Visit(node.expression);
            var type = ErrorNoType(node.token.lexeme, node.token);
            node.SetTypeName(type.identifier);
        }

        void VisitReturn(Return node) {
            Utils.RunNonNull(node.rhs, () => Visit(node.rhs));
        }

        void VerifyFunctionSignature(FunctionSignatureNode node, bool declareParams, List<TypeSymbol> paramTypes) {
            var fieldNames = new HashSet<string>();
            foreach (var param in node.parameters) {
                if (fieldNames.Contains(param.token.lexeme)) {
                    SoftError($"Function '{node.token.lexeme}' has duplicate item in parameter list named '{param.token.lexeme}'", param.token);
                } else {
                    var type = (TypeSymbol)ErrorNoType(param.type.typeName, param.token);

                    if (declareParams) {
                        DeclareSymbol(new BindingSymbol(param.token.lexeme, param.token, false, (param.isMutable) ? BindingType.Var : BindingType.Let, type));
                    }

                    param.SetTypeName(type.identifier);
                    paramTypes.Add(type);
                }

                ErrorNoType(param.type.typeName, param.type.token);
                fieldNames.Add(param.token.lexeme);
            }

            ErrorNoType(node.returnType.typeName, node.returnType.token);
            node.returnType.SetTypeName(((TypeSymbol)FindSymbol(node.returnType.token.lexeme)).identifier);
        }

        // Returns the symbol type, that doesn't match the specified type
        TypeSymbol CheckNodeHasType(AstNode node, TypeSymbol type) {
            switch (node) {
                case BinaryOpNode binop: {
                    TypeSymbol lhs = GetTypeOfNode(binop.lhs);
                    TypeSymbol rhs = GetTypeOfNode(binop.rhs);

                    if ((bool)!lhs.IsType(type)) {
                        return lhs;
                    }

                    if ((bool)!rhs.IsType(type)) {
                        return rhs;
                    }
                    break;
                }

                case UnaryOpNode unary: {
                    TypeSymbol rhs = GetTypeOfNode(unary.rhs);

                    if ((bool)!rhs.IsType(type)) {
                        return rhs;
                    }
                    break;
                }

                case LiteralNode literal: {
                    // FIXME: We can cache constants and attach their type, so they do not need to
                    //        be re-evaluated and repeatedly looking up its type
                    var expr = GetTypeFromLiteral(literal);
                    if (!expr.IsType(type)) {
                        return expr;
                    }
                    break;
                }

                case CastNode cast: {
                    var expr = new TypeSymbol(cast.token.lexeme);
                    if (!expr.IsType(type)) {
                        return expr;
                    }
                    break;
                }

                default:
                    throw new UnreachableException($"CheckNodeType - {node}");
            }

            return null;
        }

        TypeSymbol ExpectTypeOfNode(AstNode node, TypeSymbol type) {
            return CheckNodeHasType(node, type);
        }

        // Returns the type of the inferred node
        TypeSymbol InferTypeOfNode(AstNode node) {
            TypeSymbol type = GetTypeOfNode(node);

            var errorType = ExpectTypeOfNode(node, type);
            if (errorType != null) {
                SoftError($"Expression expected type '{type}' but received '{errorType}'", node.token);
            }

            return type;
        }

        TypeSymbol GetTypeFromLiteral(LiteralNode node) {
            return node.token.kind switch {
                TokenKind.Int => new TypeSymbol("int", node.token),
                TokenKind.Float => new TypeSymbol("float", node.token),
                TokenKind.True or TokenKind.False => new TypeSymbol("bool", node.token),
                TokenKind.String => new TypeSymbol("string", node.token),
                _ => throw new UnreachableException($"Analyser - {node}"),
            };
        }

        TypeSymbol GetTypeFromIdentifier(IdentifierNode node) {
            return FindSymbol(node.token.lexeme) switch {
                BindingSymbol c => c.type,
                _ => throw new UnreachableException("TypeFromIdentifier"),
            };
        }

        TypeSymbol GetTypeOfNode(AstNode node) {
            return node switch {
                LiteralNode literal => GetTypeFromLiteral(literal),
                BinaryOpNode binop => GetTypeOfNode(binop.lhs),
                UnaryOpNode unary => GetTypeOfNode(unary.rhs),
                IdentifierNode id => GetTypeFromIdentifier(id),
                BindingNode b => new TypeSymbol(b.type.typeName, b.type.token),
                ConstBindingNode b => new TypeSymbol(b.type.typeName, b.type.token),
                CastNode c => new TypeSymbol(c.token.lexeme),
                _ => throw new UnreachableException($"Analyser - {node}"),
            };
        }
    }
}