const std = @import("std");
const Allocator = std.mem.Allocator;

const Value = @import("value.zig").Value;
const Object = @import("object.zig").Object;

const Self = @This();

const Entry = struct {
    key: ?*Object.String,
    value: Value,

    pub fn isTombstone(self: *Entry) bool {
        return if (self.key != null)
            false
        else
            !self.value.isNil();
    }
};

allocator: Allocator,
entries: []Entry,
count: usize,

pub fn init(allocator: Allocator) Self {
    return .{
        .allocator = allocator,
        .entries = &[_]Entry{},
        .count = 0,
    };
}

pub fn deinit(self: *Self) void {
    self.allocator.free(self.entries);
    self.count = 0;
}

pub fn set(self: *Self, key: *Object.String, value: Value) bool {
    if (self.count + 1 >= self.entries.len) {
        self.adjust();
    }

    const entry = self.findEntry(key);
    const isNewKey = entry.key == null;
    if (isNewKey and entry.value.isNil()) self.count += 1;

    entry.key = key;
    entry.value = value;

    return isNewKey;
}

pub fn get(self: *Self, key: *Object.String) ?Value {
    if (self.count == 0) return null;

    const entry = self.findEntry(key);
    if (entry.key == null) return null;

    return entry.value;
}

pub fn delete(self: *Self, key: *Object.String) bool {
    if (self.count == 0) return false;

    const entry = self.findEntry(key);
    if (entry.key == null) return false;

    // Replace with a tombstone
    entry.key = null;
    entry.value = Value.fromNil();
    return true;
}

pub fn copyTo(self: *Self, other: *Self) void {
    for (self.entries) |entry| {
        if (entry.key) |key| {
            other.set(key, entry.value);
        }
    }
}

pub fn findString(self: *Self, buffer: []const u8, hash: u32) ?*Object.String {
    if (self.count == 0) return null;

    var index = hash & (self.entries.len - 1);
    while (true) {
        const entry = &self.entries[index];

        if (entry.key) |key| {
            if (key.hash == hash and std.mem.eql(u8, buffer, key.chars)) {
                return key;
            }
        } else if (!entry.isTombstone()) {
            // Tombstone found
            return null;
        }

        index = (index + 1) & (self.entries.len - 1);
    }
}

pub fn hasKey(self: *Self, key: []const u8) bool {
    for (0..self.entries.len) |idx| {
        const entry = &self.entries[idx];
        if (entry.key) |string| {
            if (std.mem.eql(u8, key, string.chars)) {
                return true;
            }
        }
    }

    return false;
}

fn findEntry(self: *Self, key: *Object.String) *Entry {
    var index = key.hash & (self.entries.len - 1);
    var maybe: ?*Entry = null;

    while (true) {
        const entry = &self.entries[index];
        if (entry.key) |other| {
            if (key == other) return entry;
        } else {
            if (entry.isTombstone()) {
                if (maybe == null) maybe = entry;
            } else {
                return maybe orelse entry;
            }
        }

        index = (index + 1) % self.entries.len;
    }
}

fn adjust(self: *Self) void {
    const capacity = if (self.entries.len < 8) 8 else self.entries.len << 1;
    // TODO: Handle Errors
    var entries = self.allocator.alloc(Entry, capacity) catch unreachable;

    // Initialise null entries
    for (entries) |*entry| {
        entry.key = null;
        entry.value = Value.fromNil();
    }

    // Copy old entries to new list
    self.count = 0;
    for (0..self.entries.len) |index| {
        const entry = &self.entries[index];
        if (entry.key == null) continue;

        const dest = self.findEntry(entry.key.?);
        dest.key = entry.key;
        dest.value = entry.value;
        self.count += 1;
    }

    // Cleanup and re-assign
    self.allocator.free(self.entries);
    self.entries = entries;
}
