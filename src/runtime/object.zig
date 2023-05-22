const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;
const Chunk = @import("../backend/chunk.zig").Chunk;
const Value = @import("value.zig").Value;
const VM = @import("vm.zig").VM;

pub const ObjectKind = enum {
    String,
    List,
    Function,
    Closure,
    NativeFunction,
    Upvalue,
};

pub const Object = struct {
    kind: ObjectKind,
    next: ?*Object,

    const Self = @This();

    pub fn allocate(vm: *VM, comptime T: type, kind: ObjectKind) !*Self {
        // TODO: Handle errors
        const ptr = try vm.allocator.create(T);

        ptr.object = Self{
            .kind = kind,
            .next = vm.objects,
        };

        vm.objects = &ptr.object;

        return &ptr.object;
    }

    pub fn destroy(self: *Self, vm: *VM) void {
        switch (self.kind) {
            .String => self.asString().destroy(vm),
            .List => self.asList().destroy(vm),
            .Function => self.asFunction().destroy(vm),
            .Closure => self.asClosure().destroy(vm),
            .NativeFunction => self.asNativeFunction().destroy(vm),
            .Upvalue => self.asUpvalue().destroy(vm),
        }
    }

    pub fn print(self: *Self) void {
        switch (self.kind) {
            .String => std.debug.print("{s}", .{self.asString().chars}),
            .List => {
                for (self.asList().buffer.items) |*item| {
                    item.print();
                    std.debug.print("\n", .{});
                }
            },
            .Function => std.debug.print("<fn {s}>", .{self.asFunction().getIdentifier()}),
            .Closure => std.debug.print("<closure {s}>", .{self.asClosure().function.getIdentifier()}),
            .NativeFunction => std.debug.print("<nativefn {s}>", .{self.asNativeFunction().identifier}),
            .Upvalue => std.debug.print("<upvalue>", .{}),
        }
    }

    // Check
    pub inline fn isString(self: *Self) bool {
        return self.kind == .String;
    }

    pub inline fn isList(self: *Self) bool {
        return self.kind == .List;
    }

    pub inline fn isFunction(self: *Self) bool {
        return self.kind == .Function;
    }

    pub inline fn isClosure(self: *Self) bool {
        return self.kind == .Closure;
    }

    pub inline fn isNativeFunction(self: *Self) bool {
        return self.kind == .NativeFunction;
    }

    pub inline fn isUpvalue(self: *Self) bool {
        return self.kind == .Upvalue;
    }

    // "Cast"
    pub fn asString(self: *Self) *String {
        std.debug.assert(self.isString());
        return @fieldParentPtr(String, "object", self);
    }

    pub fn asList(self: *Self) *List {
        std.debug.assert(self.isList());
        return @fieldParentPtr(List, "object", self);
    }

    pub fn asFunction(self: *Self) *Function {
        std.debug.assert(self.isFunction());
        return @fieldParentPtr(Function, "object", self);
    }

    pub fn asClosure(self: *Self) *Closure {
        std.debug.assert(self.isClosure());
        return @fieldParentPtr(Closure, "object", self);
    }

    pub fn asNativeFunction(self: *Self) *NativeFunction {
        std.debug.assert(self.isNativeFunction());
        return @fieldParentPtr(NativeFunction, "object", self);
    }

    pub fn asUpvalue(self: *Self) *Upvalue {
        std.debug.assert(self.isUpvalue());
        return @fieldParentPtr(Upvalue, "object", self);
    }

    pub const String = struct {
        object: Self,
        hash: u32,
        chars: []const u8,

        pub fn create(vm: *VM, buffer: []const u8) !*String {
            const hash = getHash(buffer);

            // Find an interned string
            if (vm.strings.findString(buffer, hash)) |str| {
                vm.allocator.free(buffer);
                return str;
            }

            const object = try Self.allocate(vm, String, .String);
            const str = object.asString();
            str.object = object.*;
            str.chars = buffer;
            str.hash = hash;

            _ = vm.strings.set(str, Value.fromBoolean(true));

            return str;
        }

        pub fn fromLiteral(vm: *VM, source: []const u8) !*String {
            const hash = getHash(source);

            // Find an interned string
            if (vm.strings.findString(source, hash)) |str| {
                return str;
            }

            const buffer = try copyLiteral(vm, source);

            const object = try Self.allocate(vm, String, .String);
            const str = object.asString();
            str.object = object.*;
            str.chars = buffer;
            str.hash = hash;

            _ = vm.strings.set(str, Value.fromBoolean(true));

            return str;
        }

        fn copyLiteral(vm: *VM, source: []const u8) ![]const u8 {
            const buffer = try vm.allocator.alloc(u8, source.len);
            std.mem.copy(u8, buffer, source);
            return buffer;
        }

        pub fn copy(vm: *VM, source: []const u8) !*String {
            return try String.create(vm, try copyLiteral(vm, source));
        }

        pub inline fn destroy(self: *String, vm: *VM) void {
            vm.allocator.free(self.chars);
            vm.allocator.destroy(self);
        }

        fn getHash(buffer: []const u8) u32 {
            var hash: u32 = 2166136261;
            for (buffer) |byte| {
                hash ^= @as(u32, byte);
                hash *%= 16777619;
            }
            return hash;
        }
    };

    pub const List = struct {
        object: Self,
        buffer: ArrayList(Value),

        pub fn create(vm: *VM, items: []const Value) !*List {
            const object = try Self.allocate(vm, List, .List);
            const list = object.asList();
            list.object = object.*;
            list.buffer = try ArrayList(Value).initCapacity(vm.allocator, if (items.len == 0) @as(usize, 8) else items.len);
            try list.buffer.appendSlice(items);

            return list;
        }

        pub inline fn destroy(self: *List, vm: *VM) void {
            self.object.asList().buffer.deinit();
            vm.allocator.destroy(self);
        }
    };

    pub const Function = struct {
        object: Self,
        arity: u8,
        chunk: Chunk,
        identifier: ?*String,

        pub fn create(vm: *VM) !*Function {
            const object = try Self.allocate(vm, Function, .Function);
            const func = object.asFunction();
            func.arity = 0;
            func.identifier = null;
            func.chunk = Chunk.init(vm.allocator);

            return func;
        }

        pub fn getIdentifier(self: *Function) []const u8 {
            if (self.identifier) |identifier| {
                return identifier.chars;
            } else {
                return "script";
            }
        }

        pub inline fn destroy(self: *Function, vm: *VM) void {
            // NOTE: The name will be handled by the GC
            self.chunk.deinit();
            vm.allocator.destroy(self);
        }
    };

    pub const Closure = struct {
        object: Self,
        function: *Function,
        // Non-owning
        upvalues: ArrayList(*Upvalue),

        pub fn create(vm: *VM, function: *Function) !*Closure {
            const object = try Self.allocate(vm, Closure, .Closure);
            const closure = object.asClosure();
            closure.function = function;
            closure.upvalues = ArrayList(*Upvalue).init(vm.allocator);
            return closure;
        }

        pub inline fn destroy(self: *Closure, vm: *VM) void {
            self.upvalues.deinit();
            self.function.destroy(vm);
            vm.allocator.destroy(self);
        }
    };

    pub const ZigFunc = *const fn (vm: *VM, args: []Value) Value;

    pub const NativeFunction = struct {
        object: Self,
        identifier: []const u8,
        arity: u8,
        function: ZigFunc,

        pub fn create(vm: *VM, identifier: []const u8, arity: u8, func: ZigFunc) !*NativeFunction {
            const object = try Self.allocate(vm, NativeFunction, .NativeFunction);
            const native = object.asNativeFunction();
            native.identifier = identifier;
            native.arity = arity;
            native.function = func;

            return native;
        }

        pub inline fn destroy(self: *NativeFunction, vm: *VM) void {
            vm.allocator.destroy(self);
        }
    };

    pub const Upvalue = struct {
        object: Self,
        location: *Value,

        pub fn create(vm: *VM, location: *Value) !*Upvalue {
            const object = try Self.allocate(vm, Upvalue, .Upvalue);
            const upvalue = object.asUpvalue();
            upvalue.location = location;
            return upvalue;
        }

        pub inline fn destroy(self: *Upvalue, vm: *VM) void {
            vm.allocator.destroy(self);
        }
    };
};
