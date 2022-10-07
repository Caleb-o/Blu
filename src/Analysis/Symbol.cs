namespace Blu {
    abstract class Symbol {
        public string identifier { get; protected set; }
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

        public bool IsType(TypeSymbol other) {
            if (identifier != other.identifier) {
                return false;
            }

            // FIXME: Check if one of the inner values is null and the other isn't

            if (inner != null && other.inner != null) {
                return identifier == other.identifier && inner.IsType(other.inner);
            }

            return identifier == other.identifier;
        }

        public override string ToString()
        {
            return (inner == null) ? identifier : $"[{inner}]";
        }
    }

    enum BindingType {
        Constant, Let, Var,
    }

    sealed class BindingSymbol : Symbol {
        public bool isPublic { get; private set; }
        public TypeSymbol? type { get; private set; }
        public BindingType bindingType { get; private set; }

        public BindingSymbol(string identifier, Token? token, bool isPublic, BindingType bindingType, TypeSymbol? type) : base(identifier, token) {
            this.isPublic = isPublic;
            this.type = type;
            this.bindingType = bindingType;
        }
    }

    sealed class FunctionSymbol : Symbol {
        public bool isPublic { get; private set; }
        public List<TypeSymbol> parameters { get; private set; }
        public TypeSymbol? ret { get; private set; }

        public FunctionSymbol(string identifier, Token token, bool isPublic, List<TypeSymbol> parameters, TypeSymbol? ret) : base(identifier, token) {
            this.isPublic = isPublic;
            this.parameters = parameters;
            this.ret = ret;
        }
    }
}