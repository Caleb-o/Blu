using System;

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
                // Interpret
            }

            Console.WriteLine("Done!");
        } catch (Exception e) when (e is LexerException ||
                                    e is ParserException ||
                                    e is AnalyserException) {
            Console.WriteLine(e.Message);
        }
    }
}