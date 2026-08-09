namespace MinimalBankSystem.Infrastructure.Persistence;

public static class DatabaseConnectionStrings
{
    public const string Name = "Database";

    public static string Require(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The required configuration key 'ConnectionStrings:Database' is not set.");
        }

        return connectionString;
    }

    public static string? FromEnvironment() =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Database")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings:Database");
}
