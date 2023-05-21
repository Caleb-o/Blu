const std = @import("std");
const Chunk = @import("chunk.zig").Chunk;

pub const ByteCode = enum(u8) {
    ConstantByte,
    ConstantShort,

    // NOTE: Will be replaced by Closure
    Closure, // index
    Call, // argument_count

    GetGlobal,
    SetGlobal,
    GetLocal,
    SetLocal,
    GetUpvalue,
    SetUpvalue,

    Not,
    Negate,
    Return,
    Pop,
    PopN, // count

    Add,
    Sub,
    Mul,
    Div,
    Pow,
    Mod,

    AddEq,
    SubEq,
    MulEq,
    DivEq,

    Nil,
    True,
    False,
    IntoList, // Count

    Puts,
    DontUse,

    const Self = @This();

    pub fn decode(self: Self, offset: u32, chunk: *Chunk) u32 {
        return switch (self) {
            .ConstantByte => constantInstruction("OP_CONSTANT_BYTE", offset, chunk),
            // FIXME: Make new short function
            .ConstantShort => constantInstruction("OP_CONSTANT_SHORT", offset, chunk),
            .Pop => simpleInstruction("OP_POP", offset),
            .PopN => byteInstruction("OP_POP_N", offset, chunk),

            .Closure => closureInstruction(offset, chunk),
            .Call => byteInstruction("OP_CALL", offset, chunk),

            .GetLocal => byteInstruction("OP_GET_LOCAL", offset, chunk),
            .SetLocal => byteInstruction("OP_SET_LOCAL", offset, chunk),

            .GetGlobal => constantInstruction("OP_GET_GLOBAL", offset, chunk),
            .SetGlobal => constantInstruction("OP_SET_GLOBAL", offset, chunk),

            .GetUpvalue => byteInstruction("OP_GET_UPVALUE", offset, chunk),
            .SetUpvalue => byteInstruction("OP_SET_UPVALUE", offset, chunk),

            .Add => simpleInstruction("OP_ADD", offset),
            .Sub => simpleInstruction("OP_SUB", offset),
            .Mul => simpleInstruction("OP_MULTIPLY", offset),
            .Div => simpleInstruction("OP_DIVIDE", offset),

            .AddEq => simpleInstruction("OP_ADD_EQ", offset),
            .SubEq => simpleInstruction("OP_SUB_EQ", offset),
            .MulEq => simpleInstruction("OP_MULTIPLY_EQ", offset),
            .DivEq => simpleInstruction("OP_DIVIDE_EQ", offset),

            .Pow => simpleInstruction("OP_POW", offset),
            .Mod => simpleInstruction("OP_MOD", offset),

            .IntoList => byteInstruction("OP_INTO_LIST", offset, chunk),
            .Nil => simpleInstruction("OP_NIL", offset),
            .True => simpleInstruction("OP_TRUE", offset),
            .False => simpleInstruction("OP_FALSE", offset),

            .Puts => byteInstruction("OP_PUTS", offset, chunk),
            .Not => simpleInstruction("OP_NOT", offset),
            .Negate => simpleInstruction("OP_NEGATE", offset),
            .Return => simpleInstruction("OP_RETURN", offset),

            else => std.debug.panic("Undefined: {}\n", .{self}),
        };
    }

    fn simpleInstruction(tag: []const u8, offset: u32) u32 {
        std.debug.print("{s}\n", .{tag});
        return offset + 1;
    }

    fn byteInstruction(comptime name: []const u8, offset: u32, chunk: *Chunk) u32 {
        const slot = chunk.code.items[offset + 1];
        std.debug.print("{s:<16} {d:4}\n", .{ name, slot });
        return offset + 2;
    }

    fn constantInstruction(tag: []const u8, offset: u32, chunk: *Chunk) u32 {
        const index = chunk.code.items[offset + 1];
        std.debug.print("{s:<16} -- '", .{tag});
        chunk.constants.items[@intCast(usize, index)].print();
        std.debug.print("'\n", .{});
        return offset + 2;
    }

    fn closureInstruction(offset: u32, chunk: *Chunk) u32 {
        const index = chunk.code.items[offset + 1];
        std.debug.print("{s:<16} -- '", .{"OP_CLOSURE"});

        var val = chunk.constants.items[@intCast(usize, index)];
        const function = val.asObject().asFunction();

        val.print();
        std.debug.print("'\n", .{});

        var upvalues = offset + 2;
        for (0..function.upvalueCount) |_| {
            const isLocal = chunk.code.items[upvalues] == 1;
            const upIndex = chunk.code.items[upvalues + 1];

            std.debug.print(
                "{d:0>4}  |                     {s} {d}\n",
                .{
                    upvalues,
                    if (isLocal) "local" else "upvalue",
                    upIndex,
                },
            );

            upvalues += 2;
        }

        return offset + 2 + (function.upvalueCount * 2);
    }
};
