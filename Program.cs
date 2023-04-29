using System;
using Blu.Runtime;

namespace Blu;

sealed class Program {
    public static CompilationUnit CompileAndRun(string path) {
        CompilationUnit unit = new Parser(path, true).Parse();
        if (!new Analyser(unit).Analyse()) {
            new Interpreter(unit).Run();
        }
        return unit;
    }

    public static void Main(string[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Usage: blu script");
            return;
        }

        try {
            CompileAndRun(args[0]);
        } catch (Exception e) when (e is LexerException ||
                                    e is ParserException ||
                                    e is AnalyserException ||
                                    e is BluException) {
            Console.WriteLine(e.Message);
        }
    }
}