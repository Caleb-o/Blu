# Blu
[![justforfunnoreally.dev badge](https://img.shields.io/badge/justforfunnoreally-dev-9ff)](https://justforfunnoreally.dev)

Simple functional style programming language prototype

Blu is a dynamic language and doesn't currently implement all FP features. It is a prototype, so it may be worked on or not.

## Example
```fs
// Recursive function (No tail-call opt)
let rec fib n =
    if n <= 1
    then n
    else fib(n-1) + fib(n-2);

let map f l = {
    let mut result = [];
    for 0 to len l = result <- result + [f(l[idx])];
    return result;
};

// Main function - this gets called by the interpreter
let main () = {
    // Mutability
    let mut a = 10;
    print a <- 100, ' ', a;

    // Everything is an expression, so you can even use let
    print let i = a <> 10;
    print a @ [3];

    // Pass a function into a function
    print map(fun x -> x * 2, [1, 2, 3]);
    print 'Fibonacci: ', fib(16);
};
```