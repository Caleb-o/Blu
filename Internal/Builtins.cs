using System;
using System.Text;
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
            }),
            CreateFunction("type_str", new string[] {"value"}, (i, args) => {
                ExpectArgsOrThrow("type_str", args, 1);
                
                return new StringValue(args[0] switch {
                    NilValue => "nil",
                    CharValue => "char",
                    StringValue => "string",
                    NumberValue => "number",
                    BoolValue => "bool",
                    ListValue => "list",
                    FunctionValue => "function",
                    NativeValue n => $"native<{n.Value.GetType()}>",
                    NativeFunctionValue => "native function",
                    _ => throw new UnreachableException("native.type_str"),
                });
            }),
            CreateFunction("platform", null, (i, args) => {
                ExpectArgsOrThrow("platform", args, 0);
                return new StringValue(System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            }),
            CreateFunction("get_date_time", null, (i, args) => {
                ExpectArgsOrThrow("get_date_time", args, 0);
                return new NativeValue(DateTime.Now);
            }),
            CreateFunction("time_since", new string[] {"time"}, (i, args) => {
                ExpectArgsOrThrow("time_since", args, 1);

                DateTime dt = ExpectOrThrowN<DateTime>("time_since", args[0]);
                return new NativeValue(DateTime.Now - dt);
            }),
            CreateFunction("time_span_s", new string[] {"time"}, (i, args) => {
                ExpectArgsOrThrow("time_span_s", args, 1);

                TimeSpan span = ExpectOrThrowN<TimeSpan>("time_span_s", args[0]);
                return new NumberValue(span.Seconds);
            }),
            CreateFunction("time_span_ms", new string[] {"time"}, (i, args) => {
                ExpectArgsOrThrow("time_span_ms", args, 1);

                TimeSpan span = ExpectOrThrowN<TimeSpan>("time_span_ms", args[0]);
                return new NumberValue(span.Milliseconds);
            }),
            CreateFunction("date_time", null, (i, args) => {
                ExpectArgsOrThrow("date_time", args, 0);
                return new StringValue(DateTime.Now.ToString());
            }),
            CreateFunction("time_ms", null, (i, args) => {
                ExpectArgsOrThrow("time_ms", args, 0);
                return new NumberValue(DateTime.Now.Millisecond);
            }),
            CreateFunction("time_s", null, (i, args) => {
                ExpectArgsOrThrow("time_s", args, 0);
                return new NumberValue(DateTime.Now.Second);
            })
        );

        Modules.Add("system", mod);
    }

    static void IOModule() {
        Module mod = new("io");
        mod.AddFunction(
            CreateFunction("exists", new string[] {"path"}, (i, args) => {
                ExpectArgsOrThrow("exists", args, 1);
                string path = ExpectOrThrowV<StringValue>("exists", args[0]).Value;
                return new BoolValue(File.Exists(path));
            }),
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
            }),
            // File based IO
            CreateFunction("open", new string[] {"path", "mod"}, (i, args) => {
                ExpectArgsOrThrow("open", args, 2);

                string path = ExpectOrThrowV<StringValue>("open", args[0]).Value;
                int mode = (int)ExpectOrThrowV<NumberValue>("open", args[1]).Value;

                if (mode < 1 || mode > 6) {
                    throw new BluException($"File mode must be 1-6, but received {mode}");
                }

                return new NativeValue(File.Open(path, (FileMode)mode));
            }),
            CreateFunction("close", new string[] {"file"}, (i, args) => {
                ExpectArgsOrThrow("close", args, 1);

                FileStream file = ExpectOrThrowN<FileStream>("close", args[0]);
                file.Close();

                return NilValue.The;
            }),
            CreateFunction("write_file", new string[] {"file", "content"}, (i, args) => {
                ExpectArgsOrThrow("write_file", args, 2);

                FileStream file = ExpectOrThrowN<FileStream>("write_file", args[0]);
                string content = ExpectOrThrowV<StringValue>("write_file", args[1]).Value;

                file.Write(Encoding.UTF8.GetBytes(content));

                return NilValue.The;
            }),
            CreateFunction("read_file", new string[] {"file"}, (i, args) => {
                ExpectArgsOrThrow("read_file", args, 1);

                FileStream file = ExpectOrThrowN<FileStream>("read_file", args[0]);
                return new StringValue(new StreamReader(file).ReadToEnd());
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
        if (value is not NativeValue || (value is NativeValue n && n.Value is not T)) {
            throw new BluException($"Expecting native object in call to '{callsite}'");
        }
        return (T)((NativeValue)value).Value;
    }

    static (string, Function) CreateFunction(string identifier, string[]? parameters, Function.InternalFunc func) =>
        (identifier, new Function(identifier, parameters, func));
}