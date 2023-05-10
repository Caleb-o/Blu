using System.Collections.Generic;

using Blu.Runtime;

namespace Blu;

// Units are used as their own AST, which are generate per-file
// Each unit knows about its source and its ast
// We also are aware if it is the entry file or an associated file
sealed class CompilationUnit {
    public readonly string fileName;
    public readonly string source;
    public readonly ProgramNode ast;
    public readonly bool isMainUnit;

    public readonly Dictionary<string, Value> exports = new();

    public CompilationUnit(string fileName, string source, ProgramNode program, bool isMainUnit) {
        this.fileName = fileName;
        this.source = source;
        this.ast = program;
        this.isMainUnit = isMainUnit;
    }
}