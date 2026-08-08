namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// Thrown when the PostgreSQL test infrastructure itself cannot be established: the container
/// runtime is unavailable, the pinned container fails to start, the server cannot be reached or
/// the started server is not the pinned PostgreSQL 18 image.
/// </summary>
/// <remarks>
/// This exception exists so that infrastructure problems surface as unambiguous test failures.
/// The fixture never converts it into a skipped test and never falls back to another provider.
/// </remarks>
public sealed class PostgresTestInfrastructureException : Exception
{
    public PostgresTestInfrastructureException()
        : base("The PostgreSQL integration test infrastructure could not be established.")
    {
    }

    public PostgresTestInfrastructureException(string message)
        : base(message)
    {
    }

    public PostgresTestInfrastructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
