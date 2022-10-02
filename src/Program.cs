namespace Blu {
    class Program {
        public static void Main(string[] args) {
            Lexer lexer = new Lexer(File.ReadAllText("examples/basic.blu"));

            Token token = lexer.Next();
            while (!token.kind.In(TokenKind.Error, TokenKind.EndOfFile)) {
                Console.WriteLine(token);
                token = lexer.Next();
            }
        }
    }
}