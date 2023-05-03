using System;
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

    public void AddOrReplace(Environment env) {
        for (int i = 0; i < Inner.Count; ++i) {
            if (Inner[i].Identifier == env.Identifier) {
                Inner[i] = env;
                return;
            }
        }

        Inner.Add(env);
    }

    public void AddIfNone(Environment env) {
        foreach (var inner in Inner) {
            if (inner.Identifier == env.Identifier) {
                return;
            }
        }

        Inner.Add(env);
    }

    public void DumpInner(int depth = 0) {
        if (depth == 0) {
            Console.WriteLine("=== ENV DUMP ===");
        }

        foreach (var inner in Inner) {
            Console.WriteLine($"{new string(' ', depth * 2)}{Identifier} :: {inner.Identifier}");
            inner.DumpInner(depth + 1);
        }
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

    public BindingSymbol? FindSymbol(Span identifier, bool visitInner = true) {
        for (int i = SymbolTable.Count - 1; i >= 0; --i) {
            var table = SymbolTable[i];

            for (int j = table.Count - 1; j >= 0; --j) {
                if (table[j].Identifier == identifier) {
                    return table[j];
                }
            }
        }

        if (visitInner) {
            foreach (var inner in Inner) {
                var sym = inner.FindSymbol(identifier, false);
                if (sym != null) return sym;
            }
        }

        return Parent?.FindSymbol(identifier, visitInner);
    }

    public void DefineSymbol(Analyser analyser, BindingSymbol sym) {
        BindingSymbol? local = FindLocalSymbol(sym.Identifier);
        if (local != null && local.Final) {
            analyser.SoftError($"Cannot overwrite '{sym.Identifier}' in current scope, as it's marked as explicit", sym.Token);
            return;
        }

        SymbolTable[^1].Add(sym);
    }

    public void BringIntoScope(Environment? env) {
        if (env == null) return;
        SymbolTable.AddRange(env.SymbolTable);
    }

    BindingSymbol? FindLocalSymbol(Span identifier) {
        if (SymbolTable.Count == 0) return null;

        for (int j = SymbolTable[^1].Count - 1; j >= 0; --j) {
            if (SymbolTable[^1][j].Identifier == identifier) {
                return SymbolTable[^1][j];
            }
        }

        return null;
    }
}