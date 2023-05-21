const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const root = @import("root");
const Token = root.lexer.Token;

const BindingKind = root.ast.BindingKind;

pub const Local = struct {
    // Use of token to report error location
    identifier: Token,
    kind: BindingKind,
    initialised: bool,
    isCaptured: bool,
    depth: u8,
    index: u8,

    const Self = @This();

    pub fn create() Self {
        return .{
            .name = undefined,
            .kind = BindingKind.None,
            .initialised = false,
            .isCaptured = false,
            .depth = 0,
            .index = 0,
        };
    }

    // Some locals require an artificial token, as a token does not exist
    // in the context of the call
    pub fn artificial(identifier: []const u8) Self {
        return .{
            .name = Token{
                .kind = .String,
                .lexeme = identifier,
                .column = 1,
                .line = 1,
            },
            .kind = BindingKind.None,
            .initialised = false,
            .depth = 0,
            .index = 0,
        };
    }
};
