const std = @import("std");
const Blu = @import("blu.zig");

pub fn main() !void {
    try Blu.run();
}

test {
    std.testing.refAllDeclsRecursive(@This());
}
