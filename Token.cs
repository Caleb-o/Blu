using System;
using System.Diagnostics.CodeAnalysis;

namespace Blu;
enum TokenKind {
    Plus, Minus, Star, Slash, Equal,
    Colon, Comma, Dot, Semicolon, LeftArrow, Arrow, At,
    Pipe, DotDot, DotLCurly,

    Greater, Less, GreaterEq, LessEq,
    NotEqual, EqualEq, And, Or,

    LCurly, RCurly,
    LParen, RParen,
    LSquare, RSquare,

    String, Number, True, False, Nil,
    
    Identifier, Let, Return, Mutable, Rec,
    If, Then, Else, Fun, For, To, Clone, Object, Final,
    Import, Export, Print, Len,// Exports identifiers into an object, which can be imported

    Error,
    EndOfFile,
}

struct Span {
    public static readonly Span Idx = new(0, 3, "idx");
    public static readonly Span Main = new(0, 4, "main");
    
    public readonly int Start, End;
    public readonly string Source;

    public Span(int start, int end, string source) {
        this.Start = start;
        this.End = end;
        this.Source = source;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) {
        Span other = (Span)obj;
        return
            End - Start == other.End - other.Start &&
            Source[Start..End] == other.Source[other.Start..other.End];
    }

    public static bool operator ==(Span obj1, Span obj2)
    {
        if (ReferenceEquals(obj1, obj2)) 
            return true;
        return obj1.Equals(obj2);
    }

    public static bool operator !=(Span obj1, Span obj2)
    {
        if (!ReferenceEquals(obj1, obj2)) 
            return true;
        return !obj1.Equals(obj2);
    }

    public override int GetHashCode() => base.GetHashCode();

    public ReadOnlySpan<char> String() => Source.AsSpan(Start, End - Start);
    public override string ToString() => String().ToString();
}

sealed class Token {
    public readonly TokenKind Kind;
    public readonly int Line;
    public readonly int Column;
    public readonly Span Span;

    public Token(TokenKind kind, int line, int column, Span span) {
        this.Kind = kind;
        this.Line = line;
        this.Column = column;
        this.Span = span;
    }

    public override string ToString() =>
        $"Token {{ kind: {Kind}, line: {Line}, column: {Column}, span: {Span} }}";

    public ReadOnlySpan<char> String() => Span.String();
}