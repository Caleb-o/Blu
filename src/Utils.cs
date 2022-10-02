namespace Blu {
    [Serializable]
    class LexerException : Exception
    {
        int line, column;

        // Should not use
        private LexerException() {}

        public LexerException(string error, int line, int column)
            : base($"Lexer error occured: '{error}' at {line}:{column}")
        {}
    }

    static class Utils {
        public static bool In<T>(this T obj, params T[] args)
        {
            return args.Contains(obj);
        }
    }
}