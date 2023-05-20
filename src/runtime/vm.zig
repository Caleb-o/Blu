const std = @import("std");
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;
const root = @import("root");

const ByteCode = root.bytecode.ByteCode;
const Chunk = root.chunk.Chunk;
const value = @import("value.zig");
const Value = value.Value;
const ValueKind = value.ValueKind;
const Object = @import("object.zig").Object;
const Compiler = root.compiler.Compiler;
const Error = root.errors;
const Table = @import("table.zig");

pub const InterpretResult = enum {
    Ok,
    CompilerError,
    RuntimeError,
};

pub const InterpretErr = error{
    InvalidOperation,
    StackUnderflow,
};

const CallFrame = struct {
    function: *Object.Function,
    ip: usize,
    slotStart: usize,

    pub fn create(function: *Object.Function, slotStart: usize) CallFrame {
        return .{
            .function = function,
            .ip = 0,
            .slotStart = slotStart,
        };
    }
};

pub const VM = struct {
    allocator: Allocator,
    errorAllocator: Allocator,
    stack: ArrayList(Value),
    frames: ArrayList(CallFrame),
    globals: Table,
    strings: Table,
    objects: ?*Object,

    const Self = @This();

    pub fn init(allocator: Allocator, errorAllocator: Allocator) !VM {
        return .{
            .allocator = allocator,
            .errorAllocator = errorAllocator,
            .stack = try ArrayList(Value).initCapacity(allocator, 32),
            .frames = try ArrayList(CallFrame).initCapacity(allocator, 8),
            .globals = Table.init(allocator),
            .strings = Table.init(allocator),
            .objects = null,
        };
    }

    pub fn deinit(self: *Self) void {
        self.frames.deinit();
        self.stack.deinit();
        self.globals.deinit();
        self.strings.deinit();
        self.freeObjects();
        self.objects = null;
    }

    pub fn setupAndRun(self: *Self, func: *Object.Function) InterpretResult {
        self.push(Value.fromObject(&func.object)) catch unreachable;

        // Call the script function
        // -- Sets up callframe
        _ = self.call(func, 0) catch {
            self.runtimeError("Failed to call script function.\n");
            return .CompilerError;
        };

        const result = self.run() catch {
            return .RuntimeError;
        };

        // Pop script function
        _ = self.pop() catch {
            self.runtimeError("Failed to empty stack on exit\n");
            return .RuntimeError;
        };
        return result;
    }

    inline fn pushFrame(self: *Self, frame: CallFrame) void {
        // TODO: Handle Errors
        self.frames.append(frame) catch unreachable;
    }

    inline fn resetStack(self: *Self) void {
        self.stack.clearAndFree();
    }

    fn freeObjects(self: *Self) void {
        var object = self.objects;
        while (object) |o| {
            const next = o.next;
            o.destroy(self);
            object = next;
        }
    }

    inline fn currentFrame(self: *Self) *CallFrame {
        return &self.frames.items[self.frames.items.len - 1];
    }

    inline fn currentChunk(self: *Self) *Chunk {
        return &self.currentFrame().function.chunk;
    }

    fn runtimeError(self: *Self, msg: []const u8) void {
        @setCold(true);

        const frame = self.currentFrame();
        _ = frame;
        // FIXME: Add line numbers
        // const line = frame.function.chunk.findOpcodeLine(frame.ip);
        const line = 1;
        std.debug.print("Error: {s} [line {d}]\n", .{ msg, line });

        var idx: isize = @intCast(isize, self.frames.items.len) - 1;
        while (idx >= 0) : (idx -= 1) {
            const stackFrame = &self.frames.items[@intCast(usize, idx)];
            const function = stackFrame.function;
            const funcLine = 1;

            std.debug.print("[line {d}] in ", .{funcLine});
            if (function.identifier) |identifier| {
                std.debug.print("{s}()\n", .{identifier.chars});
            } else {
                std.debug.print("script\n", .{});
            }
        }

        self.resetStack();
    }

    fn runtimeErrorAlloc(self: *Self, comptime fmt: []const u8, args: anytype) !void {
        @setCold(true);
        const msg = try std.fmt.allocPrint(self.errorAllocator, fmt, args);
        self.runtimeError(msg);
    }

    fn callObject(self: *Self, object: *Object, argCount: usize) !bool {
        return switch (object.kind) {
            .Function => try self.call(object.asFunction(), argCount),
            .NativeFunction => false,
            else => unreachable,
        };
    }

    fn call(self: *Self, function: *Object.Function, argCount: usize) !bool {
        if (function.arity != argCount) {
            try self.runtimeErrorAlloc(
                "Function '{s}' expected {d} arguments, but received {d}.",
                .{ function.identifier.?.chars, function.arity, argCount },
            );
            return false;
        }

        std.debug.assert(self.stack.items.len >= 1);
        self.pushFrame(CallFrame.create(
            function,
            self.stack.items.len - argCount - 1,
        ));
        return true;
    }

    fn run(self: *Self) !InterpretResult {
        defer std.debug.print("\n", .{});
        while (true) {
            const instruction = self.readByte();

            try switch (@intToEnum(ByteCode, instruction)) {
                .ConstantByte => self.push(self.readConstant()),

                .Pop => _ = try self.pop(),

                .Call => {
                    const count = self.readByte();
                    var val = self.peek(@intCast(i32, count));
                    _ = try self.callObject(val.asObject(), count);
                },

                .GetLocal => {
                    const slot = self.readByte();
                    try self.push(self.stack.items[self.currentFrame().slotStart + slot]);
                },

                .SetLocal => {
                    const slot = self.readByte();
                    self.stack.items[self.currentFrame().slotStart + slot] = self.peek(0);
                },

                .GetGlobal => {
                    const name = self.readString();
                    if (self.globals.get(name)) |val| {
                        try self.push(val);
                        continue;
                    }

                    try self.push(Value.fromNil());
                },

                .SetGlobal => {
                    const name = self.readString();
                    _ = self.globals.set(name, self.peek(0));
                },

                .Add => try self.binaryOp('+'),
                .Sub => try self.binaryOp('-'),
                .Mul => try self.binaryOp('*'),
                .Div => try self.binaryOp('/'),

                .Pow => {
                    var rhs = try self.pop();
                    var lhs = try self.pop();
                    try assertTwoType(&lhs, &rhs, .number);

                    const val = std.math.pow(f32, lhs.asNumber(), rhs.asNumber());
                    try self.push(Value.fromF32(val));
                },

                .Mod => {
                    var rhs = try self.pop();
                    var lhs = try self.pop();
                    try assertTwoType(&lhs, &rhs, .number);

                    const val = try std.math.mod(f32, lhs.asNumber(), rhs.asNumber());
                    try self.push(Value.fromF32(val));
                },

                .Nil => self.push(Value.fromNil()),
                .True => self.push(Value.fromBoolean(true)),
                .False => self.push(Value.fromBoolean(false)),

                .IntoList => {
                    const count = @intCast(usize, self.readByte());
                    var items = self.stack.items[self.stack.items.len - count ..];

                    const list = Value.fromObject(&(try Object.List.create(self, items)).object);
                    try self.stack.resize(self.stack.items.len - count);
                    try self.push(list);
                },

                .Puts => {
                    const count = @intCast(usize, self.readByte());

                    if (count > 0) {
                        for (0..count) |idx| {
                            var val = self.peek(@intCast(i32, idx));
                            val.print();
                        }
                        try self.stack.resize(self.stack.items.len - count);
                    } else {
                        var val = Value.fromNil();
                        val.print();
                    }
                    std.debug.print("\n", .{});
                },

                .Negate => {
                    var val = try self.pop();
                    if (val.isNumber()) {
                        try self.push(Value.fromF32(-val.asNumber()));
                    } else {
                        self.runtimeError("Cannot negate non-number value.");
                        return .RuntimeError;
                    }
                },

                .Not => {
                    var val = try self.pop();
                    if (val.isBoolean()) {
                        try self.push(Value.fromBoolean(!val.asBoolean()));
                    } else {
                        try self.push(Value.fromBoolean(val.falsey()));
                    }
                },

                .Return => {
                    var result = try self.pop();
                    const oldFrame = self.frames.pop();
                    if (self.frames.items.len == 0) {
                        // Script callframe - finish
                        return .Ok;
                    }
                    self.stack.resize(oldFrame.slotStart) catch unreachable;
                    _ = try self.push(result);
                },
                else => unreachable,
            };
        }
        return .Ok;
    }

    fn readByte(self: *Self) u8 {
        const frame = self.currentFrame();
        defer frame.ip += 1;
        return self.currentChunk().code.items[frame.ip];
    }

    inline fn readConstant(self: *Self) Value {
        return self.currentChunk().constants.items[self.readByte()];
    }

    inline fn readString(self: *Self) *Object.String {
        var val = self.readConstant();
        return val.asObject().asString();
    }

    pub inline fn push(self: *Self, val: Value) !void {
        _ = try self.stack.append(val);
    }

    inline fn peek(self: *Self, distance: i32) Value {
        const stack = self.stack.items;
        return stack[stack.len - 1 - @intCast(usize, distance)];
    }

    pub fn pop(self: *Self) !Value {
        if (self.stack.items.len == 0) {
            self.runtimeError("Empty stack in during pop.");
            return InterpretErr.StackUnderflow;
        }
        return self.stack.pop();
    }

    fn assertTypesEqual(lhs: *Value, rhs: *Value) !void {
        if (!lhs.compare(rhs)) {
            return InterpretErr.InvalidOperation;
        }
    }

    fn assertType(lhs: *Value, kind: ValueKind) !void {
        if (lhs.* != kind) {
            return InterpretErr.InvalidOperation;
        }
    }

    fn assertTwoType(lhs: *Value, rhs: *Value, kind: ValueKind) !void {
        if (lhs.* != kind or rhs.* != kind) {
            return InterpretErr.InvalidOperation;
        }
    }

    fn binaryOp(self: *Self, comptime op: u8) !void {
        var rhs = try self.pop();
        var lhs = try self.pop();

        // FIXME: Temporary
        try assertTypesEqual(&lhs, &rhs);

        switch (op) {
            '+' => {
                var lhsv = lhs.asNumber();
                var rhsv = rhs.asNumber();
                try self.push(Value.fromF32(lhsv + rhsv));
            },
            '-' => {
                var lhsv = lhs.asNumber();
                var rhsv = rhs.asNumber();
                try self.push(Value.fromF32(lhsv - rhsv));
            },
            '*' => {
                var lhsv = lhs.asNumber();
                var rhsv = rhs.asNumber();
                try self.push(Value.fromF32(lhsv * rhsv));
            },
            '/' => {
                var lhsv = lhs.asNumber();
                var rhsv = rhs.asNumber();
                try self.push(Value.fromF32(lhsv / rhsv));
            },
            else => unreachable,
        }
    }
};
