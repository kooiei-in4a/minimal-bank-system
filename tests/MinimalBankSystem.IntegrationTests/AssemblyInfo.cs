using Xunit;

// Default: tests in different collections may run in parallel.
// Shared mutable state (e.g. Console capture in API contract tests) must use a
// dedicated xUnit collection so those tests serialize with each other.
// PostgreSQL integration tests use a shared container and a unique database per
// test, so they may run in parallel across test classes.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
