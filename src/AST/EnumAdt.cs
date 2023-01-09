using System.Collections.Generic;

namespace Blu {
    abstract class AdtField {}

    sealed class NumericField : AdtField {
        public readonly int Value;

        public NumericField(int value) {
            this.Value = value;
        }
    }

    sealed class TupleField : AdtField {
        public readonly TypeNode[] Types;

        public TupleField(TypeNode[] types) {
            this.Types = types;
        }
    }

    sealed class EnumAdt : AstNode {
        public readonly bool IsPublic;
        public readonly (Token, AdtField)[] Fields;

        public EnumAdt(bool isPublic, Token token, (Token, AdtField)[] fields) : base(token) {
            this.IsPublic = isPublic;
            this.Fields = fields;
        }
    }
}