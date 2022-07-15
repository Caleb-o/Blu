const std = @import("std");
const token = @import("lexing/token.zig");

pub fn main() anyerror!void {
    const t = token.Token.init(1, 1, token.Span.init(0, 0), token.TokenKind.plus);
    std.debug.print("{}", .{t.span.start});
}

test "basic test" {
    try std.testing.expectEqual(10, 3 + 7);
}
