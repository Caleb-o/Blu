using System;
using Blu.Runtime;

namespace Blu;

sealed class Program {
    public static void Main(string[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Usage: blu script");
            return;
        }

        try {
            CompilationUnit unit = new Parser(args[0], true).Parse();
            if (!new Analyser(unit).Analyse()) {
                new Interpreter().Run(unit);
            }

            Console.WriteLine("Done!");
        } catch (Exception e) when (e is LexerException ||
                                    e is ParserException ||
                                    e is AnalyserException ||
                                    e is BluException) {
            Console.WriteLine(e.Message);
        }
    }
}