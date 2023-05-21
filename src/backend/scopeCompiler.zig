const std = @import("std");
const ArrayList = std.ArrayList;
const root = @import("root");
const Object = root.object.Object;

const VM = root.vm.VM;
const Local = @import("locals.zig").Local;

// Functions and lambdas are treated differently
// Functions cannot view locals outside its scope,
// only globals, whereas a lambda will capture.
pub const ScopeKind = enum { Script, Function, Lambda };

pub const Upvalue = struct {
    index: u8,
    isLocal: bool,

    pub fn create() Upvalue {
        return .{
            .index = 0,
            .isLocal = false,
        };
    }
};

pub const ScopeCompiler = struct {
    enclosing: ?*ScopeCompiler,
    kind: ScopeKind,
    function: *Object.Function,
    depth: usize,
    locals: ArrayList(Local),
    upvalues: ArrayList(Upvalue),

    const Self = @This();

    pub fn init(vm: *VM, depth: usize, kind: ScopeKind, enclosing: ?*ScopeCompiler) !Self {
        return .{
            .enclosing = enclosing,
            .kind = kind,
            .function = try Object.Function.create(vm),
            .depth = depth,
            .locals = ArrayList(Local).init(vm.allocator),
            .upvalues = ArrayList(Upvalue).init(vm.allocator),
        };
    }

    pub fn deinit(self: *Self) void {
        self.locals.deinit();
        self.upvalues.deinit();
    }
};
