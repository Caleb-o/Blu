namespace Blu {
    // Units are used as their own AST, which are generate per-file
    // Each unit knows about its source and its ast
    // We also are aware if it is the entry file or an associated file
    class CompilationUnit {
        public string? fileName { get; private set; }
        public string? source { get; private set; }
        public ProgramNode? ast { get; private set; }
        public bool isMainUnit { get; private set; }

        public CompilationUnit(string fileName, string source, ProgramNode program, bool isMainUnit) {
            this.fileName = fileName;
            this.source = source;
            this.ast = program;
            this.isMainUnit = isMainUnit;
        }
    }
}