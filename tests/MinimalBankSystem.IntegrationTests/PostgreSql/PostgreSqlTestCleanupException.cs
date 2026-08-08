namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Raised when test database cleanup fails. Failures are never swallowed.
/// </summary>
internal sealed class PostgreSqlTestCleanupException : Exception
{
    public PostgreSqlTestCleanupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
