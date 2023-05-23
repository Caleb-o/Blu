const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;

const errors = @import("../errors.zig");
const Token = @import("../frontend/lexer.zig").Token;

const BindingKind = @import("../frontend/nodes/ast.zig").BindingKind;
const CompilerError = errors.CompilerError;
const ScopeCompiler = @import("scopeCompiler.zig").ScopeCompiler;

pub const Local = struct {
    // Use of token to report error location
    identifier: Token,
    kind: BindingKind,
    initialised: bool,
    depth: u8,
    index: u8,

    const Self = @This();

    pub fn create() Self {
        return .{
            .name = undefined,
            .kind = BindingKind.None,
            .initialised = false,
            .depth = 0,
            .index = 0,
        };
    }

    pub inline fn isGlobal(self: *Self) bool {
        return self.depth == 0;
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
