const std = @import("std");
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;

const debug = @import("../debug.zig");

const ScopeCompiler = @import("../backend/scopeCompiler.zig").ScopeCompiler;
const VM = @import("vm.zig").VM;
const Value = @import("value.zig").Value;
const object = @import("object.zig");
const Object = object.Object;
const ObjectKind = object.ObjectKind;
const Table = @import("table.zig");

pub const GC = struct {
    inner: Allocator,
    vm: *VM,
    bytesAllocated: usize,
    nextSweep: usize,

    const SWEEP_FACTOR: usize = 2;

    const Self = @This();

    pub fn init(inner: Allocator, vm: *VM) Self {
        return .{
            .inner = inner,
            .vm = vm,
            .bytesAllocated = 0,
            .nextSweep = 1024 * 1024,
        };
    }

    pub fn allocator(self: *Self) Allocator {
        return Allocator{
            .ptr = self,
            .vtable = &.{
                .alloc = alloc,
                .resize = resize,
                .free = free,
            },
        };
    }

    fn alloc(
        ctx: *anyopaque,
        n: usize,
        log2_ptr_align: u8,
        ret_addr: usize,
    ) ?[*]u8 {
        const self = @ptrCast(*Self, @alignCast(@alignOf(Self), ctx));
        if ((self.bytesAllocated + n > self.nextSweep) or debug.STRESS_GC) {
            self.collectGarbage() catch |err| {
                std.debug.print("GC Error: '{s}'\n", .{@errorName(err)});
                std.os.exit(1);
            };

            self.nextSweep = self.bytesAllocated * SWEEP_FACTOR;
        }

        var out = self.inner.rawAlloc(n, log2_ptr_align, ret_addr) orelse return null;
        self.bytesAllocated += n;

        return out;
    }

    fn resize(
        ctx: *anyopaque,
        buf: []u8,
        buf_align: u8,
        new_len: usize,
        ret_addr: usize,
    ) bool {
        const self = @ptrCast(*Self, @alignCast(@alignOf(Self), ctx));
        if (new_len > buf.len) {
            if ((self.bytesAllocated + (new_len - buf.len) > self.nextSweep) or debug.STRESS_GC) {
                self.collectGarbage() catch |err| {
                    std.debug.print("GC Error: '{s}'\n", .{@errorName(err)});
                    std.os.exit(1);
                };
            }
        }

        const resized = self.inner.rawResize(buf, buf_align, new_len, ret_addr);

        if (resized) {
            if (new_len > buf.len) {
                self.bytesAllocated += new_len - buf.len;
            } else {
                self.bytesAllocated -= buf.len - new_len;
            }
        }

        return resized;
    }

    fn free(
        ctx: *anyopaque,
        buf: []u8,
        buf_align: u8,
        ret_addr: usize,
    ) void {
        const self = @ptrCast(*Self, @alignCast(@alignOf(Self), ctx));
        self.inner.rawFree(buf, buf_align, ret_addr);
        self.bytesAllocated -= buf.len;

        if (debug.LOG_GC) {
            std.debug.print("Freeing {d} bytes\n", .{buf.len});
        }
    }

    fn markObject(self: *Self, obj: *Object) !void {
        if (obj.marked) return;

        obj.marked = true;
        try self.vm.greyList.append(obj);

        if (debug.LOG_GC) {
            std.debug.print("marked: '", .{});
            obj.print();
            std.debug.print("'\n", .{});
        }
    }

    fn markTable(self: *Self, table: *Table) !void {
        for (0..table.count) |idx| {
            const entry = &table.entries[idx];
            try self.markObject(&entry.key.?.object);
            try self.markValue(&entry.value);
        }
    }

    inline fn markValue(self: *Self, value: *Value) !void {
        if (value.isObject()) {
            try self.markObject(value.asObject());
        }
    }

    fn markCompilerRoots(self: *Self) !void {
        if (self.vm.compiler) |compiler| {
            var scopeCompiler: ?*ScopeCompiler = compiler.scopeComp;
            while (scopeCompiler) |current| {
                try self.markObject(&current.function.object);
                scopeCompiler = current.enclosing.?;
            }
        }
    }

    fn markRoots(self: *Self) !void {
        for (self.vm.stack.items) |*item| {
            try self.markValue(item);
        }

        for (self.vm.frames.items) |frame| {
            if (frame.closure) |closure| {
                try self.markObject(&closure.object);
            } else {
                try self.markObject(&frame.function.object);
            }
        }

        var upvalue = self.vm.openUpvalues;
        while (upvalue) |current| {
            try self.markObject(&current.object);
            upvalue = current.next.?;
        }

        try self.markCompilerRoots();
        try self.markTable(&self.vm.globals);
    }

    fn markArrayList(self: *Self, list: *ArrayList(Value)) !void {
        for (list.items) |*value| {
            try self.markValue(value);
        }
    }

    fn blackenObject(self: *Self, obj: *Object) !void {
        if (debug.LOG_GC) {
            std.debug.print("blacken: '", .{});
            obj.print();
            std.debug.print("'\n", .{});
        }

        switch (obj.kind) {
            .Upvalue => try self.markValue(&obj.asUpvalue().closed),

            .Function => {
                const function = obj.asFunction();
                try self.markObject(&function.identifier.?.object);
                try self.markArrayList(&function.chunk.constants);
            },

            .Closure => {
                const closure = obj.asClosure();
                try self.markObject(&closure.function.object);
                for (closure.upvalues) |upvalue| {
                    try self.markObject(&upvalue.object);
                }
            },

            .NativeFunction, .String => {},
            else => unreachable,
        }
    }

    fn traceReferences(self: *Self) !void {
        for (self.vm.greyList.items) |grey| {
            try self.blackenObject(grey);
        }
    }

    fn sweep(self: *Self) !void {
        var previous: ?*Object = null;
        var maybeObject = self.vm.objects;

        while (maybeObject) |obj| {
            if (obj.marked) {
                obj.marked = false;
                previous = obj;
                maybeObject = obj.next;
            } else {
                const unreached = obj;
                maybeObject = obj.next;
                if (previous) |prev| {
                    prev.next = obj;
                } else {
                    self.vm.objects = obj;
                }

                unreached.destroy(self.vm);
            }
        }
    }

    fn removeWhiteStrings(self: *Self, table: *Table) !void {
        _ = self;
        for (table.entries) |*entry| {
            if (entry.key) |key| {
                if (!key.object.marked) {
                    _ = table.delete(key);
                }
            }
        }
    }

    fn collectGarbage(self: *Self) !void {
        if (debug.LOG_GC) {
            std.debug.print("-- gc begin\n", .{});
            defer std.debug.print("-- gc end\n", .{});
        }

        try self.markRoots();
        try self.traceReferences();
        try self.removeWhiteStrings(&self.vm.strings);
        try self.sweep();
    }
};
