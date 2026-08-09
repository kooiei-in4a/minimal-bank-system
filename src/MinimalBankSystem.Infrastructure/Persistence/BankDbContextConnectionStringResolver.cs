namespace MinimalBankSystem.Infrastructure.Persistence;

public static class BankDbContextConnectionStringResolver
{
    public const string ConnectionStringConfigurationKey = "ConnectionStrings:Database";

    public const string ConnectionStringEnvironmentVariableName = "ConnectionStrings__Database";

    public static string Resolve()
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The '{ConnectionStringEnvironmentVariableName}' environment variable is not set. " +
                $"MinimalBankSystem requires an explicit PostgreSQL connection string for " +
                $"'{ConnectionStringConfigurationKey}' and does not fall back to SQLite, InMemory, " +
                "or another provider.");
        }

        return connectionString;
    }
}
