namespace Blu {
    abstract class Symbol {
        public string? identifier { get; protected set; }
        // Builtins will not have tokens
        public Token? token { get; protected set; }

        public Symbol(string identifier, Token? token = null) {
            this.identifier = identifier;
            this.token = token;
        }

        public bool IsBuiltin() => token == null;
    }

    sealed class TypeSymbol : Symbol {
        // Inner is for arrays
        public TypeSymbol? inner { get; private set; }

        public TypeSymbol(string identifier, Token? token = null, TypeSymbol? inner = null) : base(identifier, token) {
            this.inner = inner;
        }
    }

    sealed class FunctionSymbol : Symbol {
        public List<TypeSymbol> parameters { get; private set; }
        public TypeSymbol ret { get; private set; }

        public FunctionSymbol(string identifier, Token token, List<TypeSymbol> parameters, TypeSymbol ret) : base(identifier, token) {
            this.parameters = parameters;
            this.ret = ret;
        }
    }
}