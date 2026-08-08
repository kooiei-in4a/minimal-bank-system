using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Proves cleanup failures surface as hard failures and are not swallowed.
/// </summary>
[Trait("Category", PostgreSqlTestCategories.Category)]
public sealed class PostgreSqlCleanupFailureTests
{
    [Fact]
    public async Task CleanupFailureIsNotSwallowed()
    {
        PostgreSqlTestDatabase database = await PostgreSqlTestDatabase.CreateAsync();
        await using NpgsqlConnection openSession = new(database.ConnectionString);
        await openSession.OpenAsync();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.CleanupAsync(terminateBackends: false));

        Assert.Contains(
            $"Failed to clean up PostgreSQL test database '{database.DatabaseName}'",
            failure.Message,
            StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);

        await openSession.DisposeAsync();
        await database.CleanupAsync(terminateBackends: true);
        database.MarkCleaned();
    }
}
