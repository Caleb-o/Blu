using System;
using System.IO;
using System.Linq;

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

    [Serializable]
    class AnalyserException : Exception
    {
        // Should not AnalyserException
        private AnalyserException() {}

        public AnalyserException(string fileName, string error, int line, int column)
            : base($"Error occured: {error} in '{fileName}' at {line}:{column}")
        {}
    }

    [Serializable]
    class BluException : Exception
    {
        // Should not AnalyserException
        private BluException() {}

        public BluException(string message)
            : base($"Internal error: {message}")
        {}
    }

    [Serializable]
    class UnreachableException : Exception
    {
        // Should not use
        private UnreachableException() {}

        public UnreachableException(string where)
            : base($"Unreachable case at '{where}'")
        {}
    }

    static class Utils {
        public static bool In<T>(this T obj, params T[] args)
        {
            return args.Contains(obj);
        }

        public static void InternalError(string message) {
            throw new BluException(message);
        }

        public static void RunNonNull(object item, Action fn) {
            if (item != null) fn();
        }

        public static (bool, string) CheckInEnvPath(string fileName)
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return new(true, fullPath);
            }
            return new(false, null);
        }
    }
}