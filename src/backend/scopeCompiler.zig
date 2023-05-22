const std = @import("std");
const root = @import("root");
const Object = @import("../runtime/object.zig").Object;

const VM = @import("../runtime/vm.zig").VM;

// Functions and lambdas are treated differently
// Functions cannot view locals outside its scope,
// only globals, whereas a lambda will capture.
pub const ScopeKind = enum { Script, Function, Lambda };

pub const ScopeCompiler = struct {
    enclosing: ?*ScopeCompiler,
    kind: ScopeKind,
    function: *Object.Function,
    depth: usize,
    locals: u8,

    const Self = @This();

    pub fn init(vm: *VM, depth: usize, kind: ScopeKind, enclosing: ?*ScopeCompiler) !Self {
        return .{
            .enclosing = enclosing,
            .kind = kind,
            .function = try Object.Function.create(vm),
            .depth = depth,
            .locals = 0,
        };
    }
};
