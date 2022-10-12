using System.Diagnostics;

namespace Blu {
    class Program {
        public static void Main(string[] args) {
            if (args.Length != 1) {
                Console.WriteLine("Usage: blue script");
                return;
            }

            try {
                Parser parser = new Parser(args[0], true);
                var unit = parser.Parse();

                Analyser analyser = new Analyser(unit);
                if (!analyser.Analyse()) {
                    Generator gen = new Generator();
                    gen.Generate(unit);
                }

                // TODO: Add flag to enable C# compilation, rather than running by default
                TryCompileCSharp();

                Console.WriteLine("Done!");
            } catch (Exception e) when (e is LexerException ||
                                        e is ParserException ||
                                        e is AnalyserException) {
                Console.WriteLine(e.Message);
            }
        }

        static void TryCompileCSharp() {
            // Compile C# code if csc exists
            var csc = Utils.CheckInEnvPath("csc.exe");
            if (csc.Item1) {
                // Compile the file
                Console.Write("Compiling C#...");
                var compileProc = new Process() {
                    StartInfo = {
                        FileName = csc.Item2,
                        UseShellExecute = false,
                        Arguments = "./out.blucs -warn:0 -o -out:out.exe",
                        RedirectStandardOutput = true,
                    },
                };
                compileProc.Start();
                compileProc.WaitForExit();
                
                Console.WriteLine("Done!");
            }
        }
    }
}