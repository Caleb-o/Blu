using System.Collections.Generic;

namespace Blu.Internal;

sealed class Module {
    public readonly string Identifier;
    public readonly Dictionary<string, Function> Functions = new();

    public Module(string identifier) {
        this.Identifier = identifier;
    }

    public void AddFunction(params (string, Function)[] functions) {
        foreach (var f in functions) {
            if (!Functions.TryAdd(f.Item1, f.Item2)) {
                throw new BluException($"Cannot add function '{f.Item1}' to module '{Identifier}'");
            }
        }
    }
}