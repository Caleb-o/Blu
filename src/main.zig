const std = @import("std");
const GPA = std.heap.GeneralPurposeAllocator;
const blu = @import("blu.zig");

pub fn main() !void {
    var gpa = GPA(.{}){};
    const allocator = gpa.allocator();

    defer {
        const leak = gpa.deinit();
        if (leak == .leak) std.debug.panic("Allocator leaked!\n", .{});
    }

    try blu.bluMain(allocator);
}
