using System.Collections.Generic;

namespace Blu {
    abstract class Symbol {
        public readonly string identifier;
        // Builtins will not have tokens
        public readonly Token token;

        public Symbol(string identifier, Token token) {
            this.identifier = identifier;
            this.token = token;
        }

        public bool IsBuiltin() => token == null;
    }

    sealed class BindingSymbol : Symbol {
        public readonly bool Mutable;
        public BindingSymbol(Token token, string identifier, bool mutable) : base(identifier, token) {
            this.Mutable = mutable;
        }
    }
}