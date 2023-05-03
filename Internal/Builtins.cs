using System.Collections.Generic;
using Blu.Runtime;

namespace Blu.Internal;

static class Builtins {
    public static readonly Dictionary<string, Module> Modules = new();

    static Builtins() {
        IOModule();
    }

    static void IOModule() {
        Module mod = new("io");
        mod.AddFunction(
            CreateFunction("write", new string[] {"path", "content"}, (i, args) => { return NilValue.The; })
        );

        Modules.Add("io", mod);
    }

    static (string, Function) CreateFunction(string identifier, string[]? parameters, Function.InternalFunc func) =>
        (identifier, new Function(identifier, parameters, func));
}