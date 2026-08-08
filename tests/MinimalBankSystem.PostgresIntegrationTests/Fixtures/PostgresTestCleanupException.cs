namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// Thrown when tearing down an isolated test database does not complete successfully.
/// </summary>
/// <remarks>
/// Cleanup failures are never swallowed. Because the owning test disposes its database inside
/// <see cref="PostgresIntegrationTest.DisposeAsync"/>, this exception is reported by xUnit as a
/// failure of the test that leaked the database.
/// </remarks>
public sealed class PostgresTestCleanupException : Exception
{
    public PostgresTestCleanupException()
        : base("An isolated PostgreSQL test database could not be cleaned up.")
    {
    }

    public PostgresTestCleanupException(string message)
        : base(message)
    {
    }

    public PostgresTestCleanupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PostgresTestCleanupException(string databaseName, string message, Exception innerException)
        : base(message, innerException)
    {
        DatabaseName = databaseName;
    }

    /// <summary>The database whose cleanup failed, when it is known.</summary>
    public string? DatabaseName { get; }
}
