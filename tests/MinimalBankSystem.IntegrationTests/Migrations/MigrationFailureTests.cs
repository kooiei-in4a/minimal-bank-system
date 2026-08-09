extern alias Migrator;

using Npgsql;
using MigrationExecutor = Migrator::MinimalBankSystem.Migrator.MigrationExecutor;

namespace MinimalBankSystem.IntegrationTests.Migrations;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class MigrationFailureTests
{
    [Fact]
    public async Task MissingConnectionConfigurationFailsExplicitlyWithoutFallback()
    {
        MigrationProcessResult result = await MigrationProcessHarness.RunMigratorAsync(connectionString: null);

        Assert.NotEqual(MigrationExecutor.ExitSuccess, result.ExitCode);
        Assert.Contains("not configured", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("migrated successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnreachableDatabaseFailsWithoutProviderFallbackOrSecretLeak()
    {
        const string password = "test-only-unused-password";
        NpgsqlConnectionStringBuilder unreachable = new()
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "postgres",
            Username = "postgres",
            Password = password,
            Pooling = false,
            Timeout = 2,
            CommandTimeout = 2,
        };

        MigrationProcessResult result = await MigrationProcessHarness.RunMigratorAsync(unreachable.ConnectionString);

        Assert.NotEqual(MigrationExecutor.ExitSuccess, result.ExitCode);
        Assert.Contains("migration failed", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("migrated successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrationExecutorConvertsCancellationToFailureExitCode()
    {
        using CancellationTokenSource canceled = new();
        canceled.Cancel();

        int exitCode = await MigrationExecutor.ExecuteAsync(
            "Host=127.0.0.1;Database=unused;Pooling=false;Timeout=5",
            canceled.Token);

        Assert.Equal(MigrationExecutor.ExitFailure, exitCode);
    }
}
