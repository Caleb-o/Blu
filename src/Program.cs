namespace Blu {
    class Program {
        public static void Main(string[] args) {
            try {
                Parser parser = new Parser("examples/basic.blu", true);
                _ = parser.Parse();
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }
    }
}