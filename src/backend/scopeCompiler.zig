const std = @import("std");
const root = @import("root");
const Object = root.object.Object;

const VM = root.vm.VM;

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
    depth: i32,
    locals: u8,
    upvalues: []Upvalue,

    const Self = @This();

    pub fn init(vm: *VM, depth: i32, kind: ScopeKind, enclosing: ?*ScopeCompiler) !Self {
        return .{
            .enclosing = enclosing,
            .kind = kind,
            .function = try Object.Function.create(vm),
            .depth = depth,
            .locals = 0,
            .upvalues = &[_]Upvalue{
                Upvalue.create(),
            } ** (std.math.maxInt(u8) + 1),
        };
    }
};
