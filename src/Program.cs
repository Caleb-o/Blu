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
            } catch (Exception e) when (e is LexerException ||
                                        e is ParserException ||
                                        e is AnalyserException) {
                Console.WriteLine(e.Message);
            }
        }
    }
}