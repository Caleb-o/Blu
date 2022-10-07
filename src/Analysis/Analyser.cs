namespace Blu {
    sealed class Analyser {
        List<List<Symbol>> symbolTable = new List<List<Symbol>>();
        bool hadError = false;
        CompilationUnit unit;

        public Analyser(CompilationUnit unit) {
            this.unit = unit;

            symbolTable.Add(new List<Symbol>());
            DefineBuiltinTypes();
        }

        public bool Analyse() {
            Visit(this.unit.ast);
            return this.hadError;
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

        void Visit(AstNode node) {
            switch (node) {
                case ProgramNode n:
                    VisitProgram(n);
                    break;

                case BodyNode n:
                    VisitBody(n);
                    break;

                case StructNode n:
                    VisitStruct(n);
                    break;
                
                default:
                    throw new UnreachableException("Analyser - Visit");
            }
        }

        void VisitProgram(ProgramNode node) {
            Visit(node.body);
        }

        void VisitBody(BodyNode node) {
            foreach (var n in node.statements) {
                Visit(n);
            }
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
    }
}