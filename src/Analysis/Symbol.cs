using System.Collections.Generic;

namespace Blu {
    abstract class Symbol {
        public string identifier { get; protected set; }
        // Builtins will not have tokens
        public Token token { get; protected set; }

        public Symbol(string identifier, Token token = null) {
            this.identifier = identifier;
            this.token = token;
        }

        public bool IsBuiltin() => token == null;
    }

    // TODO:
    /*
        Type information should know if it can be coerced into another type.
        eg. Foo class can coerce to object

        Type information should know if it can successfully convert to other types
        eg. (int) 1 can coerce to (float) 1.0
    */
    sealed class TypeSymbol : Symbol {
        // Inner is for arrays
        public readonly TypeSymbol inner;
        readonly Dictionary<string, TypeSymbol> fields = new();

        public TypeSymbol(string identifier, Token token = null, TypeSymbol inner = null) : base(identifier, token) {
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

        public bool HasField(string fieldName) {
            return fields.ContainsKey(fieldName);
        }

        public TypeSymbol GetField(string fieldName) {
            return (fields.ContainsKey(fieldName))
                ? fields[fieldName]
                : null;
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
        public readonly bool isPublic;
        public readonly TypeSymbol type;
        public readonly BindingType bindingType;

        public BindingSymbol(string identifier, Token token, bool isPublic, BindingType bindingType, TypeSymbol type) : base(identifier, token) {
            this.isPublic = isPublic;
            this.type = type;
            this.bindingType = bindingType;
        }
    }

    sealed class FunctionSymbol : Symbol {
        public readonly bool isPublic;
        public readonly List<TypeSymbol> parameters;
        public readonly TypeSymbol ret;

        public FunctionSymbol(string identifier, Token token, bool isPublic, List<TypeSymbol> parameters, TypeSymbol ret) : base(identifier, token) {
            this.isPublic = isPublic;
            this.parameters = parameters;
            this.ret = ret;
        }
    }

    sealed class EnumSymbol : Symbol
    {
        readonly HashSet<string> FieldNames;

        public EnumSymbol(string identifier, HashSet<string> fieldNames, Token token = null) : base(identifier, token) {
            this.FieldNames = fieldNames;
        }
    }

    sealed class TraitSymbol : Symbol {
        public readonly bool isPublic;
        public readonly List<FunctionSymbol> functions;

        public TraitSymbol(string identifier, Token token, bool isPublic, List<FunctionSymbol> functions) : base(identifier, token) {
            this.isPublic = isPublic;
            this.functions = functions;
        }

        FunctionSymbol FindFunction(string identifier) {
            foreach (var fn in functions) {
                if (fn.identifier == identifier) {
                    return fn;
                }
            }

            return null;
        }
    }
}