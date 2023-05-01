using System.Collections.Generic;

namespace Blu.Analysis;

sealed class Environment {
    public readonly string Identifier;
    public readonly Environment? Parent;
    public readonly List<List<BindingSymbol>> SymbolTable = new() { new() };

    public readonly List<Environment> Inner = new();

    public Environment(string identifier, Environment? parent = null) {
        this.Identifier = identifier;
        this.Parent = parent;
    }

    public Environment? FindEnv(string identifier, bool lookupParent = true) {
        if (identifier == Identifier) {
            return this;
        }

        foreach (var other in Inner) {
            if (other.Identifier == identifier) {
                return other;
            }
            Environment? inner = other.FindEnv(identifier, false);
            if (inner != null) return inner;
        }

        return lookupParent ? Parent?.FindEnv(identifier) : null;
    }

    public BindingSymbol? FindSymbol(Span identifier) {
        for (int i = SymbolTable.Count - 1; i >= 0; --i) {
            var table = SymbolTable[i];

            for (int j = table.Count - 1; j >= 0; --j) {
                if (table[j].Identifier == identifier) {
                    return table[j];
                }
            }
        }

        return Parent?.FindSymbol(identifier);
    }

    public void DefineSymbol(Analyser analyser, BindingSymbol sym) {
        BindingSymbol? local = FindLocalSymbol(sym.Identifier);
        if (local != null && local.Explicit) {
            analyser.SoftError($"Cannot overwrite '{sym.Identifier}' in current scope, as it's marked as explicit", sym.Token);
            return;
        }

        SymbolTable[SymbolTable.Count - 1].Add(sym);
    }

    public void BringIntoScope(Environment? env) {
        if (env == null) return;
        SymbolTable.AddRange(env.SymbolTable);
    }

    BindingSymbol? FindLocalSymbol(Span identifier) {
        for (int j = SymbolTable[SymbolTable.Count - 1].Count - 1; j >= 0; --j) {
            if (SymbolTable[SymbolTable.Count - 1][j].Identifier == identifier) {
                return SymbolTable[SymbolTable.Count - 1][j];
            }
        }

        return null;
    }
}