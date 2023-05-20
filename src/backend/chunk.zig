const std = @import("std");
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;

const root = @import("root");
const Value = root.value.Value;
const ByteCode = root.bytecode.ByteCode;

/// A chunk is a compiled object that defines code within functions
/// or other scoped types that may require its own chunk.
/// It is also self-contained with its referenced constants.
/// Passed by value and are held in run-time instances.
pub const Chunk = struct {
    code: ArrayList(u8),
    constants: ArrayList(Value),

    const Self = @This();

    pub fn init(allocator: Allocator) Self {
        return .{
            .code = ArrayList(u8).init(allocator),
            .constants = ArrayList(Value).init(allocator),
        };
    }

    pub fn deinit(self: *Self) void {
        self.constants.deinit();
        self.code.deinit();
    }

    pub fn write(self: *Self, byte: u8) !void {
        try self.code.append(byte);
    }

    pub fn findOrInsert(self: *Chunk, value: Value) !usize {
        // FIXME: Find a better method for checking hashes
        //        This will be required for later, as all objects need a hash too!
        try self.constants.append(value);
        return self.constants.items.len - 1;
    }

    pub inline fn addConstant(self: *Chunk, value: Value) !usize {
        const index = try self.findOrInsert(value);
        return @intCast(u8, index);
    }

    pub inline fn disassemble(self: *Chunk, name: []const u8) void {
        var index: u32 = 0;
        const len = @intCast(u32, self.code.items.len);

        std.debug.print("=== {s} : {d} ===\n", .{ name, self.code.items.len });

        while (index < len) {
            std.debug.print("{d:0>4}  ", .{index});

            var opcode = @intToEnum(ByteCode, self.code.items[index]);
            index = opcode.decode(index, self);
        }
    }
};
