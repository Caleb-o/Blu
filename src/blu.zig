const std = @import("std");
const fs = std.fs;
const Allocator = std.mem.Allocator;

pub fn bluMain(allocator: Allocator) !void {
    const source = try loadFile(allocator, "./tmp.txt");
    defer allocator.free(source);
    std.debug.print("Source: '{s}'\n", .{source});
}

fn loadFile(allocator: Allocator, filePath: []const u8) ![]const u8 {
    const file = try fs.cwd().openFile(filePath, .{});
    defer file.close();
    const source = try file.readToEndAlloc(allocator, (try file.stat()).size);
    return source;
}
