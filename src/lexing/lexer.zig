const std = @import("std");
const Allocator = std.mem.Allocator;

// var arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
// defer arena.deinit();
// const allocator = arena.allocator();
pub const Lexer = struct {
    ip: usize,
    line: usize,
    column: u16,
    allocator: Allocator,

    // Take an allocator, so the creator can define the allocator of the internal arena
    pub fn init(allocator: Allocator) Lexer {
        return Lexer { 
            .ip = 0, 
            .line = 1, .column = 1,
            .allocator = std.heap.ArenaAllocator.init(allocator).allocator,
        };
    }
};