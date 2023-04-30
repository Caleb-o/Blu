using System.Collections.Generic;

namespace Blu {
    sealed class BindingSymbol {
        public readonly Span Identifier;
        // Builtins will not have tokens
        public readonly Token Token;

        public readonly bool Explicit;
        public readonly bool Mutable;
        public BindingSymbol(Token token, Span identifier, bool exp, bool mutable) {
            this.Token = token;
            this.Identifier = identifier;
            this.Explicit = exp;
            this.Mutable = mutable;
        }
    }
}