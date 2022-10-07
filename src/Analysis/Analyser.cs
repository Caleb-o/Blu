namespace Blu {
    sealed class Analyser {
        List<List<Symbol>> symbolTable = new List<List<Symbol>>();
        bool hadError = false;
        CompilationUnit unit;

        public Analyser(CompilationUnit unit) {
            this.unit = unit;

            PushScope();
            DefineBuiltinTypes();
        }

        public bool Analyse() {
            Visit(this.unit.ast);
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

            Console.WriteLine($"Error occured: {message} in {this.unit?.fileName} at {token.line}:{token.column}");
        }

        // Note: This error will cause the compilation to stop entirely
        void Error(string message, Token token) {
            throw new ParserException(this.unit?.fileName, message, (int)token.line, (int)token.column);
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

        void ErrorNoType(string identifier, Token token) {
            var foundType = FindSymbol(identifier);
            if (foundType == null) {
                TypeError(identifier, token);
            }
        }

        Symbol? FindSymbol(string identifier) {
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
                case ProgramNode n:
                    VisitProgram(n);
                    break;

                case BodyNode n:
                    VisitBody(n, true);
                    break;

                case StructNode n:
                    VisitStruct(n);
                    break;

                case FunctionNode n:
                    VisitFunction(n);
                    break;

                case BindingNode n:
                    VisitBinding(n);
                    break;
                
                case ConstBindingNode n:
                    VisitConstBinding(n);
                    break;

                case IdentifierNode n:
                    VisitIdentifier(n);
                    break;
                
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
            List<TypeSymbol> parameterTypes = new List<TypeSymbol>();
            TypeSymbol? ret = null;

            DeclareSymbol(new FunctionSymbol(node.token.lexeme, node.token, parameterTypes, ret));
            PushScope();

            var fieldNames = new HashSet<string>();
            foreach (var param in node.parameters) {
                if (fieldNames.Contains(param.token.lexeme)) {
                    SoftError($"Function '{node.token.lexeme}' has duplicate item in parameter list named '{param.token.lexeme}'", param.token);
                } else {
                    TypeSymbol type = (TypeSymbol)FindSymbol(param.type.typeName);
                    DeclareSymbol(new BindingSymbol(param.token.lexeme, param.token, (param.isMutable) ? BindingType.Var : BindingType.Let, type));
                }

                ErrorNoType(param.type.typeName, param.type.token);
                fieldNames.Add(param.token.lexeme);
            }

            ErrorNoType(node.returnType.typeName, node.returnType.token);

            VisitBody(node.body, false);

            PopScope();
        }

        void VisitBinding(BindingNode node) {
            var type = (TypeSymbol)FindSymbol(node.type?.token.lexeme);
            DeclareSymbol(new BindingSymbol(node.token.lexeme, node.token, GetBindingType(node), type));

            // FIXME: Visit expression
        }

        void VisitConstBinding(ConstBindingNode node) {
            var type = (TypeSymbol)FindSymbol(node.type?.token.lexeme);
            DeclareSymbol(new BindingSymbol(node.token.lexeme, node.token, BindingType.Constant, type));

            // FIXME: Visit expression
        }

        void VisitIdentifier(IdentifierNode node) {
            var id = FindSymbol(node.token.lexeme);
            SoftError($"Identifier '{node.token.lexeme}' does not exist", node.token);
        }
    }
}