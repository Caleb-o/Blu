namespace Blu {
    class Program {
        public static void Main(string[] args) {
            if (args.Length != 1) {
                Console.WriteLine("Usage: blue script");
                return;
            }

            try {
                Parser parser = new Parser(args[0], true);
                _ = parser.Parse();
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }
    }
}