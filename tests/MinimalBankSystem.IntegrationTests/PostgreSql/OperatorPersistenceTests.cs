using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>Real PostgreSQL evidence for the WP2-ID-01 persistence contract.</summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorPersistenceTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset SeedUtc =
        new(2026, 8, 14, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task PreviousFoundationMigrationUpgradesToLatestIdentitySchema()
    {
        await using (BankDbContext context = CreateContext())
        {
            await context.Database.MigrateAsync("20260809113338_InitialFoundation");

            Assert.Equal(
                ["20260809113338_InitialFoundation"],
                [.. await context.Database.GetAppliedMigrationsAsync()]);
            Assert.False(await TableExistsAsync("Operators"));

            await context.Database.MigrateAsync();

            Assert.Equal(
                [
                    "20260809113338_InitialFoundation",
                    "20260813181851_OperatorIdentityPersistence",
                ],
                [.. await context.Database.GetAppliedMigrationsAsync()]);
        }

        Assert.True(await TableExistsAsync("Operators"));
    }

    [Fact]
    public async Task OperatorIdentityRoundTripsWithIdentityHashAndSecurityStampState()
    {
        MigratorRun migration = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        Operator seeded = OperatorTestSeed.Create("integration-operator");
        await using (BankDbContext context = CreateContext())
        {
            context.Operators.Add(seeded);
            await context.SaveChangesAsync();
        }

        await using (BankDbContext context = CreateContext())
        {
            Operator loaded = Assert.Single(await context.Operators.AsNoTracking().ToListAsync());

            Assert.Equal(seeded.Id, loaded.Id);
            Assert.Equal('7', seeded.Id.ToString("D")[14]);
            Assert.Equal("integration-operator", loaded.UserName);
            Assert.Equal("INTEGRATION-OPERATOR", loaded.NormalizedUserName);
            Assert.Equal(OperatorState.Active, loaded.State);
            Assert.Equal(OperatorRole.Administrator, loaded.Role);
            Assert.Equal(Operator.InitialAuthorizationStateVersion, loaded.AuthorizationStateVersion);
            Assert.Equal(SeedUtc, loaded.CreatedAt);
            Assert.Equal(SeedUtc, loaded.UpdatedAt);
            Assert.NotNull(loaded.PasswordHash);
            Assert.NotEqual(OperatorTestSeed.PlaintextPassword, loaded.PasswordHash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                OperatorTestSeed.PasswordHasher.VerifyHashedPassword(
                    loaded,
                    loaded.PasswordHash!,
                    OperatorTestSeed.PlaintextPassword));
            Assert.False(string.IsNullOrWhiteSpace(loaded.SecurityStamp));
            Assert.False(string.IsNullOrWhiteSpace(loaded.ConcurrencyStamp));
        }

        string[] columns = await ReadOperatorColumnsAsync();
        Assert.Equal(
            [
                "Id",
                "State",
                "Role",
                "AuthorizationStateVersion",
                "CreatedAt",
                "UpdatedAt",
                "UserName",
                "NormalizedUserName",
                "PasswordHash",
                "SecurityStamp",
                "ConcurrencyStamp",
            ],
            columns);

        Assert.DoesNotContain(
            "AspNetRoles",
            await ReadPublicTablesAsync(),
            StringComparer.Ordinal);
        Assert.DoesNotContain(
            "AspNetUserRoles",
            await ReadPublicTablesAsync(),
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("active", "", 1, "role-empty")]
    [InlineData("active", "administrator,viewer", 1, "role-multiple")]
    [InlineData("unknown", "administrator", 1, "state-unknown")]
    [InlineData("active", "administrator", 0, "authorization-version-zero")]
    public async Task DatabaseRejectsInvalidOperatorInvariantValues(
        string state,
        string role,
        long authorizationStateVersion,
        string suffix)
    {
        MigratorRun migration = await MigratorProcess.RunAsync(
            Database.ConnectionString,
            TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, migration.ExitCode);

        await using NpgsqlConnection connection = await PostgreSqlContainerFixture.OpenConnectionAsync(
            Database.ConnectionString,
            $"checking invalid Operator invariant '{suffix}'");
        await using NpgsqlCommand command = new(
            """
            INSERT INTO "Operators" (
                "Id", "State", "Role", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt",
                "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "ConcurrencyStamp")
            VALUES ($1, $2, $3, $4, $5, $5, $6, $7, $8, $9, $10);
            """,
            connection);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(state);
        command.Parameters.AddWithValue(role);
        command.Parameters.AddWithValue(authorizationStateVersion);
        command.Parameters.AddWithValue(SeedUtc);
        command.Parameters.AddWithValue($"{suffix}-user");
        command.Parameters.AddWithValue($"{suffix}-user".ToUpperInvariant());
        command.Parameters.AddWithValue("AQAAAAIAAYagAAAAEidentity-test-hash");
        command.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal("23514", exception.SqlState);
        Assert.Empty(await ReadOperatorUserNamesAsync());
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString);
        return new BankDbContext(options.Options);
    }

    private Task<string[]> ReadPublicTablesAsync() =>
        ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """);

    private Task<string[]> ReadOperatorColumnsAsync() =>
        ReadStringsAsync(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Operators'
            ORDER BY ordinal_position;
            """);

    private Task<string[]> ReadOperatorUserNamesAsync() =>
        ReadStringsAsync("SELECT \"UserName\" FROM \"Operators\";");

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using NpgsqlConnection connection = await PostgreSqlContainerFixture.OpenConnectionAsync(
            Database.ConnectionString,
            $"checking table '{tableName}'");
        await using NpgsqlCommand command = new(
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = $1);
            """,
            connection);
        command.Parameters.AddWithValue(tableName);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private async Task<string[]> ReadStringsAsync(string commandText)
    {
        await using NpgsqlConnection connection = await PostgreSqlContainerFixture.OpenConnectionAsync(
            Database.ConnectionString,
            "reading Operator persistence evidence");
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static class OperatorTestSeed
    {
        public const string PlaintextPassword = "integration-only-password";

        public static readonly PasswordHasher<Operator> PasswordHasher = new();

        public static Operator Create(string userName)
        {
            Operator identity = Operator.Create(
                userName,
                PlaintextPassword,
                OperatorRole.Administrator,
                SeedUtc,
                PasswordHasher);
            return identity;
        }
    }
}
