using System.IO;
using System.Collections.Generic;
using Blu.Runtime;

namespace Blu.Internal;

static class Builtins {
    public static readonly Dictionary<string, Module> Modules = new();

    static Builtins() {
        SystemModule();
        IOModule();
    }

    static void SystemModule() {
        Module mod = new("system");
        mod.AddFunction(
            CreateFunction("assert", new string[] {"condition", "message"}, (i, args) => {
                ExpectArgsOrThrow("assert", args, 2);

                bool condition = ExpectOrThrowV<BoolValue>("assert", args[0]).Value;
                string message = ExpectOrThrowV<StringValue>("assert", args[1]).Value;

                if (!condition) {
                    throw new BluException($"Assert: {message}");
                }

                return NilValue.The;
            })
        );

        Modules.Add("system", mod);
    }

    static void IOModule() {
        Module mod = new("io");
        mod.AddFunction(
            CreateFunction("read", new string[] {"path"}, (i, args) => {
                ExpectArgsOrThrow("read", args, 1);

                string path = ExpectOrThrowV<StringValue>("read", args[0]).Value;

                try {
                    return new StringValue(File.ReadAllText(path));
                } catch (IOException) {
                    return NilValue.The;
                }
            }),
            CreateFunction("write", new string[] {"path", "content"}, (i, args) => {
                ExpectArgsOrThrow("write", args, 2);

                string path = ExpectOrThrowV<StringValue>("write", args[0]).Value;
                string content = ExpectOrThrowV<StringValue>("write", args[1]).Value;

                File.WriteAllText(path, content);
                return NilValue.The;
            })
        );

        Modules.Add("io", mod);
    }

    static void ExpectArgsOrThrow(string callsite, Value[] args, int arity) {
        if (args.Length != arity) {
            throw new BluException($"Native function '{callsite}' expects {arity} args, but received {args.Length}");
        }
    }

    static T ExpectOrThrowV<T>(string callsite, Value value) {
        if (value is not T) {
            throw new BluException($"Incorrect type provided in call to '{callsite}'");
        }
        return (T)value;
    }

    static T ExpectOrThrowN<T>(string callsite, Value value) {
        if (value is not NativeValue || (value is NativeValue n && n.Value is T)) {
            throw new BluException($"Expecting native object in call to '{callsite}'");
        }
        return (T)((NativeValue)value).Value;
    }

    static (string, Function) CreateFunction(string identifier, string[]? parameters, Function.InternalFunc func) =>
        (identifier, new Function(identifier, parameters, func));
}