const std = @import("std");
const ArrayList = std.ArrayList;
const Allocator = std.mem.Allocator;
const Object = @import("object.zig").Object;

pub const ValueKind = enum {
    nil,
    number,
    boolean,
    object,
};

pub const Value = union(ValueKind) {
    nil: void,
    number: f32,
    boolean: bool,
    object: *Object,

    const Self = @This();

    // Constructors
    pub fn fromNil() Self {
        return .{
            .nil = {},
        };
    }

    pub fn fromF32(value: f32) Self {
        return .{
            .number = value,
        };
    }

    pub fn fromBoolean(value: bool) Self {
        return .{
            .boolean = value,
        };
    }

    pub fn fromObject(value: *Object) Self {
        return .{
            .object = value,
        };
    }

    // Destructors
    pub fn destroy(self: *Self, allocator: Allocator) void {
        switch (self) {
            .object => |v| v.destroy(allocator),
            else => {},
        }
    }

    // Casts
    pub inline fn isNil(self: *Self) bool {
        return self.* == .nil;
    }

    pub inline fn isNumber(self: *Self) bool {
        return self.* == .number;
    }

    pub inline fn isBoolean(self: *Self) bool {
        return self.* == .boolean;
    }

    pub inline fn isObject(self: *Self) bool {
        return self.* == .object;
    }

    pub fn asNumber(self: *Self) f32 {
        std.debug.assert(self.isNumber());
        return self.number;
    }

    pub fn asBoolean(self: *Self) bool {
        std.debug.assert(self.isBoolean());
        return self.boolean;
    }

    pub fn asObject(self: *Self) *Object {
        std.debug.assert(self.isObject());
        return self.object;
    }

    // Utilities
    pub fn falsey(self: *Self) bool {
        return switch (self.*) {
            .nil => true,
            .boolean => |v| !v,
            else => false,
        };
    }

    pub fn compare(self: *Self, other: *Self) bool {
        return switch (self.*) {
            .number => switch (other.*) {
                .number => true,
                else => false,
            },
            else => false,
        };
    }

    pub fn print(self: *Self) void {
        switch (self.*) {
            .nil => std.debug.print("nil", .{}),
            .number => |v| std.debug.print("{d}", .{v}),
            .boolean => |v| std.debug.print("{any}", .{v}),
            .object => |v| v.print(),
        }
    }

    pub fn getHash(self: Self) u32 {
        return switch (self) {
            .nil => 0,
            .number => |v| @floatToInt(u32, v),
            .boolean => |v| if (v) 1 else 0,
            .object => 0,
        };
    }
};
