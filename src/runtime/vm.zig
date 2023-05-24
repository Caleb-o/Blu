const std = @import("std");
const Allocator = std.mem.Allocator;
const FixedBufferAllocator = std.heap.FixedBufferAllocator;
const ArrayList = std.ArrayList;

const debug = @import("../debug.zig");
const ByteCode = @import("../backend/bytecode.zig").ByteCode;
const Chunk = @import("../backend/chunk.zig").Chunk;
const value = @import("value.zig");
const Value = value.Value;
const ValueKind = value.ValueKind;
const Object = @import("object.zig").Object;
const Error = @import("../errors.zig");
const RuntimeError = Error.RuntimeError;
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
    closure: ?*Object.Closure,
    ip: usize,
    slotStart: usize,

    pub fn create(function: *Object.Function, slotStart: usize) CallFrame {
        return .{
            .function = function,
            .closure = null,
            .ip = 0,
            .slotStart = slotStart,
        };
    }

    pub fn createWithClosure(closure: *Object.Closure, slotStart: usize) CallFrame {
        return .{
            .function = closure.function,
            .closure = closure,
            .ip = 0,
            .slotStart = slotStart,
        };
    }
};

// The buffer should be a fixed size and live on the
// *real* stack, so the access is cheap
var STACK_BUFFER: [256 * @sizeOf(Value)]u8 = undefined;

pub const VM = struct {
    allocator: Allocator,
    stackAllocator: Allocator,
    errorAllocator: Allocator,
    stack: ArrayList(Value),
    frames: ArrayList(CallFrame),
    globals: Table,
    strings: Table,
    openUpvalues: ?*Object.Upvalue,
    objects: ?*Object,

    const Self = @This();

    pub fn init(allocator: Allocator, errorAllocator: Allocator) !VM {
        var fba = FixedBufferAllocator.init(&STACK_BUFFER);
        const stackAllocator = fba.allocator();
        return .{
            .allocator = allocator,
            .stackAllocator = stackAllocator,
            .errorAllocator = errorAllocator,
            .stack = try ArrayList(Value).initCapacity(stackAllocator, 32),
            .frames = try ArrayList(CallFrame).initCapacity(allocator, 8),
            .globals = Table.init(allocator),
            .strings = Table.init(allocator),
            .openUpvalues = null,
            .objects = null,
        };
    }

    pub fn deinit(self: *Self) void {
        self.frames.deinit();
        self.globals.deinit();
        self.strings.deinit();
        self.freeObjects();
        self.objects = null;
    }

    pub fn setupAndRun(self: *Self, func: *Object.Function) !InterpretResult {
        try self.push(Value.fromObject(&func.object));

        // Call the script function
        // -- Sets up callframe
        _ = self.callFunction(func, 0) catch {
            self.runtimeError("Failed to call script function.\n");
            return .CompilerError;
        };

        const result = self.run() catch {
            self.runtimeError("Failed to run\n");
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
        self.stack.clearRetainingCapacity();
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

    fn captureUpvalue(self: *Self, local: *Value) RuntimeError!*Object.Upvalue {
        var prevUpvalue: ?*Object.Upvalue = null;
        var maybeUpvalue = self.openUpvalues;

        while (maybeUpvalue) |upvalue| {
            if (@ptrToInt(upvalue.location) <= @ptrToInt(local)) break;
            prevUpvalue = upvalue;
            maybeUpvalue = upvalue.next;
        }

        // Check if the value points to the same location in memory
        if (maybeUpvalue) |upvalue| {
            if (upvalue.location == local) {
                return upvalue;
            }
        }

        const createdUpvalue = try Object.Upvalue.create(self, local);
        createdUpvalue.next = maybeUpvalue;

        // Insert into the VM
        if (prevUpvalue) |p| {
            p.next = createdUpvalue;
        } else {
            self.openUpvalues = createdUpvalue;
        }

        return createdUpvalue;
    }

    fn closeUpvalues(self: *Self, last: *Value) !void {
        while (self.openUpvalues) |openUpvalues| {
            if (@ptrToInt(openUpvalues.location) < @ptrToInt(last)) break;
            const upvalue = openUpvalues;
            upvalue.closed = upvalue.location.*;
            upvalue.location = &upvalue.closed;
            self.openUpvalues = upvalue.next;
        }
    }

    fn callObject(self: *Self, object: *Object, argCount: usize) !bool {
        return switch (object.kind) {
            .Closure => try self.call(object.asClosure(), argCount),
            .Function => try self.callFunction(object.asFunction(), argCount),
            .NativeFunction => {
                // Don't include function within args
                const args = self.stack.items[self.stack.items.len - argCount ..];
                const result = object.asNativeFunction().function(self, args);

                try self.stack.resize(self.stack.items.len - 1 - argCount);
                try self.push(result);
                return true;
            },
            else => {
                self.runtimeError("Cannot call value");
                return RuntimeError.InvalidCallOnValue;
            },
        };
    }

    fn callFunction(self: *Self, function: *Object.Function, argCount: usize) !bool {
        if (function.arity != argCount) {
            try self.runtimeErrorAlloc(
                "Function '{s}' expected {d} arguments, but received {d}.",
                .{ function.getIdentifier(), function.arity, argCount },
            );
            return false;
        }

        std.debug.assert(self.stack.items.len >= 1);
        self.pushFrame(CallFrame.create(
            function,
            self.stack.items.len - argCount,
        ));
        return true;
    }

    fn call(self: *Self, closure: *Object.Closure, argCount: usize) !bool {
        if (closure.function.arity != argCount) {
            try self.runtimeErrorAlloc(
                "Function '{s}' expected {d} arguments, but received {d}.",
                .{ closure.function.getIdentifier(), closure.function.arity, argCount },
            );
            return false;
        }

        std.debug.assert(self.stack.items.len >= 1);
        self.pushFrame(CallFrame.createWithClosure(
            closure,
            self.stack.items.len - argCount,
        ));
        return true;
    }

    fn run(self: *Self) !InterpretResult {
        if (debug.PRINT_CODE) {
            defer std.debug.print("\n", .{});
        }
        while (true) {
            const instruction = self.readByte();
            try switch (@intToEnum(ByteCode, instruction)) {
                .ConstantByte => self.push(self.readConstant()),

                .Pop => _ = try self.pop(),

                .Function => {
                    var val = self.readConstant();
                    try self.push(Value.fromObject(val.asObject()));
                },

                .Closure => {
                    var val = self.readConstant();
                    const function = val.asObject().asFunction();
                    const closure = try Object.Closure.create(self, function);
                    try self.push(Value.fromObject(&closure.object));

                    for (closure.upvalues) |*upvalue| {
                        const isLocal = self.readByte() != 0;
                        const index = self.readByte();

                        const frame = self.currentFrame();
                        if (isLocal) {
                            upvalue.* = try self.captureUpvalue(&self.stack.items[frame.slotStart + index]);
                        } else {
                            upvalue.* = frame.closure.?.upvalues[index];
                        }
                    }
                },

                .Call => {
                    const count = self.readByte();
                    var func = self.peek(@intCast(i32, count));
                    _ = try self.callObject(func.asObject(), count);
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

                    try self.runtimeErrorAlloc(
                        "Unknown global '{s}'",
                        .{name.chars},
                    );
                    return RuntimeError.UndefinedGlobal;
                },

                .SetGlobal => {
                    const name = self.readString();
                    _ = self.globals.set(name, self.peek(0));
                },

                .GetUpvalue => {
                    const slot = @intCast(usize, self.readByte());
                    const frame = self.currentFrame();
                    try self.push(frame.closure.?.upvalues[slot].location.*);
                },

                .SetUpvalue => {
                    const slot = @intCast(usize, self.readByte());
                    const frame = self.currentFrame();
                    std.debug.print("Setting at slot {d}:'{s}'\n", .{
                        slot,
                        frame.function.getIdentifier(),
                    });
                    frame.closure.?.upvalues[slot].location.* = self.peek(0);
                },

                .CloseUpvalue => {
                    try self.closeUpvalues(&self.stack.items[self.stack.items.len - 2]);
                    _ = try self.pop();
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

                .Out => {
                    const count = @intCast(usize, self.readByte());
                    std.debug.print("OUT: ", .{});

                    if (count > 0) {
                        for (0..count) |idx| {
                            var val = self.peek(@intCast(i32, count - 1 - idx));
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

                    try self.closeUpvalues(&self.stack.items[oldFrame.slotStart - 1]);

                    if (self.frames.items.len == 0) {
                        // Script callframe - finish
                        return .Ok;
                    }

                    try self.stack.resize(oldFrame.slotStart + 1);
                    try self.push(result);
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

    pub fn push(self: *Self, val: Value) RuntimeError!void {
        if (self.stack.items.len >= std.math.maxInt(u8)) {
            self.runtimeError("Stack overflow");
            return RuntimeError.StackOverflow;
        }
        try self.stack.append(val);
    }

    pub inline fn peek(self: *Self, distance: i32) Value {
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

    fn assertTypesEqual(self: *Self, lhs: *Value, rhs: *Value) !void {
        if (!lhs.compare(rhs)) {
            self.runtimeError(
                "Left-hand side type did not match right-hand side",
            );
            lhs.print();
            std.debug.print(" ", .{});
            rhs.print();
            std.debug.print("\n", .{});
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
        try self.assertTypesEqual(&lhs, &rhs);

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
