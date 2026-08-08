using Xunit;

// Parallel policy for the real PostgreSQL integration tests.
//
// Parallel range:   test classes (xUnit collections) run in parallel. Every test owns a private
//                   database on the shared container, so nothing is shared and mutable between
//                   them.
// Serialized range: tests inside one collection never overlap. Cluster-scoped tests declare that
//                   by joining PostgresClusterCollection. CREATE DATABASE and DROP DATABASE are
//                   additionally serialized inside PostgresTestServer.
// Thread bound:     capped so the number of concurrent databases and backends stays well below the
//                   container's default max_connections, and so the bound is identical on a
//                   developer machine and on a CI runner instead of following the core count.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]

// Gives the shared container a deterministic assembly-scoped teardown.
[assembly: TestFramework(
    "MinimalBankSystem.PostgresIntegrationTests.Fixtures.PostgresTestFramework",
    "MinimalBankSystem.PostgresIntegrationTests")]
