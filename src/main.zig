const std = @import("std");
const Allocator = std.mem.Allocator;
const GPA = std.heap.GeneralPurposeAllocator(.{});
const Arena = std.heap.ArenaAllocator;

// Expose to root
pub const lexer = @import("frontend/lexer.zig");
pub const ast = @import("frontend/nodes/ast.zig");
pub const parser = @import("frontend/parser.zig");
pub const bytecode = @import("backend/bytecode.zig");
pub const chunk = @import("backend/chunk.zig");
pub const compiler = @import("backend/compiler.zig");
pub const errors = @import("errors.zig");
pub const debug = @import("debug.zig");
pub const value = @import("runtime/value.zig");
pub const object = @import("runtime/object.zig");
pub const vm = @import("runtime/vm.zig");

pub fn main() void {
    langMain() catch {
        std.debug.print("Error occured!\n", .{});
    };
}

fn langMain() !void {
    var alloc = GPA{};
    const allocator = alloc.allocator();
    defer {
        const leaked = alloc.deinit();
        if (leaked == .leak) std.debug.panic("LEAKED\n", .{});
    }

    const args = try std.process.argsAlloc(allocator);
    defer std.process.argsFree(allocator, args);

    if (args.len != 2) {
        std.debug.print("Usage: concat [fileName]\n", .{});
        return;
    }

    const source = try readFile(allocator, args[1]);
    defer allocator.free(source);

    try parseAndGo(source);
}

fn parseAndGo(source: []const u8) !void {
    // Arena here has a massive improvement over other allocators
    // Since it's single free
    var arena = Arena.init(std.heap.page_allocator);
    const arenaAlloc = arena.allocator();
    defer arena.deinit();

    var pars = parser.Parser.init(arenaAlloc, source);
    defer pars.deinit();

    var root = try pars.parse();

    var machine = try vm.VM.init(arenaAlloc, arenaAlloc);
    defer machine.deinit();

    var comp = compiler.Compiler.init(&machine);

    const func = try comp.run(root);
    const result = try machine.setupAndRun(func);

    if (debug.PRINT_CODE) {
        std.debug.print("Run result: {s}\n", .{@tagName(result)});
    }
}

fn readFile(allocator: Allocator, fileName: []const u8) ![]u8 {
    const file = try std.fs.cwd().openFile(fileName, .{});
    const contents = try file.readToEndAlloc(allocator, (try file.stat()).size);
    return contents;
}
