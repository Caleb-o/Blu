namespace Blu {
    [Serializable]
    class LexerException : Exception
    {
        // Should not use
        private LexerException() {}

        public LexerException(string error, int line, int column)
            : base($"Error occured: {error} at {line}:{column}")
        {}
    }

    [Serializable]
    class ParserException : Exception
    {
        // Should not use
        private ParserException() {}

        public ParserException(string fileName, string error, int line, int column)
            : base($"Error occured: {error} in '{fileName}' at {line}:{column}")
        {}
    }

    static class Utils {
        public static bool In<T>(this T obj, params T[] args)
        {
            return args.Contains(obj);
        }
    }
}