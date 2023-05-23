const std = @import("std");
const Allocator = std.mem.Allocator;
const GPA = std.heap.GeneralPurposeAllocator(.{});
const Arena = std.heap.ArenaAllocator;

const debug = @import("debug.zig");
const Parser = @import("frontend/parser.zig").Parser;
const Compiler = @import("backend/compiler.zig").Compiler;
const _VM = @import("runtime/vm.zig");
const VM = _VM.VM;
const InterpretErr = _VM.InterpretErr;

pub fn run() !void {
    var alloc = GPA{};
    const allocator = alloc.allocator();
    defer {
        const leaked = alloc.deinit();
        if (leaked == .leak) std.debug.panic("LEAKED\n", .{});
    }

    const args = try std.process.argsAlloc(allocator);
    defer std.process.argsFree(allocator, args);

    if (args.len != 2) {
        std.debug.print("Usage: blu [fileName]\n", .{});
        return;
    }

    const source = try readFile(allocator, args[1]);
    defer allocator.free(source);

    parseAndGo(source) catch |err| {
        std.debug.print("Error occured: {s}\n", .{@errorName(err)});
    };
}

fn parseAndGo(source: []const u8) !void {
    // Arena here has a massive improvement over other allocators
    // Since it's single free
    var arena = Arena.init(std.heap.page_allocator);
    const arenaAlloc = arena.allocator();
    defer arena.deinit();

    var parser = Parser.init(arenaAlloc, source);
    defer parser.deinit();

    var root = try parser.parse();

    var vm = try VM.init(arenaAlloc, arenaAlloc);
    defer vm.deinit();

    var compiler = Compiler.init(&vm);

    const func = try compiler.run(root);
    const result = vm.setupAndRun(func) catch |err| {
        std.debug.print("Run err: {s}\n", .{@errorName(err)});
        return InterpretErr.InvalidOperation;
    };

    if (debug.PRINT_CODE) {
        std.debug.print("Run result: {s}\n", .{@tagName(result)});
    }
}

fn readFile(allocator: Allocator, fileName: []const u8) ![]u8 {
    const file = try std.fs.cwd().openFile(fileName, .{});
    const contents = try file.readToEndAlloc(allocator, (try file.stat()).size);
    return contents;
}

test {
    std.testing.refAllDeclsRecursive(@This());
}
