const std = @import("std");
const print = std.debug.print;

const lexer = @import("frontend/lexer.zig");
const Token = lexer.Token;

pub const ParserError = error{
    UnknownSymbol,
    MalformedCode,
} || std.mem.Allocator.Error;

pub const CompilerError = error{
    TooManyLocals,
    TooManyArguments,
    TooManyConstants,
    TooManyUpvalues,
    TooManyScopes,
    UndefinedLocal,
    UnknownLocal,
    LocalDefined,

    Unimplemented,
} || std.mem.Allocator.Error || std.fmt.ParseFloatError;

pub const RuntimeError = error{
    StackOverflow,
    StackUnderflow,
    InvalidCallOnValue,
} || std.mem.Allocator.Error;

pub fn errorWithToken(token: *const Token, where: []const u8, msg: []const u8) void {
    print("[Error:{s}] {s} '{s}' [{d}:{d}]\n", .{ where, msg, token.lexeme, token.line, token.column });
}
