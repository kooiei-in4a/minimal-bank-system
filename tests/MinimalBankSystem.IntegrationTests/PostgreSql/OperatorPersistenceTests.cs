using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.IntegrationTests.Identity;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Real PostgreSQL evidence for the WP2-ID-01 Operator persistence invariants required by
/// Issue #165: application-generated UUIDv7 id, active/disabled and exactly-one-fixed-role check
/// constraints (including explicit zero-role and invalid/multiple-role rejection), unique login
/// identifier, ASP.NET Core Identity password hashing with no plaintext persistence, and UTC
/// timestamptz / authorization-state-version / security-stamp round-trip.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorPersistenceTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly TimeProvider FixedTimeProvider =
        new FakeTimeProvider(DateTimeOffset.Parse("2026-08-13T18:00:00Z", CultureInfo.InvariantCulture));

    [Fact]
    public async Task SeededOperatorsForAllThreeFixedRolesRoundTripThroughTheOperatorsTable()
    {
        await using BankDbContext write = await CreateMigratedContextAsync();

        Operator administrator = OperatorSeedData.CreateAdministrator(FixedTimeProvider);
        Operator teller = OperatorSeedData.CreateTeller(FixedTimeProvider);
        Operator viewer = OperatorSeedData.CreateViewer(FixedTimeProvider);
        write.Operators.AddRange(administrator, teller, viewer);
        await write.SaveChangesAsync();

        await using BankDbContext read = await CreateMigratedContextAsync();
        Operator[] reloaded =
        [
            .. await read.Operators.OrderBy(o => o.UserName).ToListAsync(),
        ];

        Assert.Equal(3, reloaded.Length);
        AssertRoundTripped(administrator, Assert.Single(reloaded, o => o.Id == administrator.Id));
        AssertRoundTripped(teller, Assert.Single(reloaded, o => o.Id == teller.Id));
        AssertRoundTripped(viewer, Assert.Single(reloaded, o => o.Id == viewer.Id));

        static void AssertRoundTripped(Operator expected, Operator actual)
        {
            Assert.Equal(expected.UserName, actual.UserName);
            Assert.Equal(expected.NormalizedUserName, actual.NormalizedUserName);
            Assert.Equal(expected.Role, actual.Role);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.PasswordHash, actual.PasswordHash);
            Assert.Equal(expected.SecurityStamp, actual.SecurityStamp);
            Assert.Equal(expected.AuthorizationStateVersion, actual.AuthorizationStateVersion);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
            Assert.Equal(TimeSpan.Zero, actual.CreatedAt.Offset);
            Assert.Equal(TimeSpan.Zero, actual.UpdatedAt.Offset);
        }
    }

    [Fact]
    public async Task PasswordHashRoundTripsThroughRealIdentityHashingAndPlaintextIsNeverPersisted()
    {
        const string plaintext = "Correct-Horse-Battery-Staple-9";
        await using BankDbContext write = await CreateMigratedContextAsync();
        Operator created = OperatorFactory.Create(FixedTimeProvider, "hash-roundtrip-user", plaintext, OperatorRole.Teller);
        write.Operators.Add(created);
        await write.SaveChangesAsync();

        string? storedHash = await ExecuteScalarAsync<string>(
            $"SELECT \"PasswordHash\" FROM \"{OperatorConfiguration.TableName}\" WHERE \"Id\" = @id;",
            ("id", created.Id));

        Assert.NotNull(storedHash);
        Assert.NotEqual(plaintext, storedHash);
        Assert.DoesNotContain(plaintext, storedHash, StringComparison.Ordinal);

        await using BankDbContext read = await CreateMigratedContextAsync();
        Operator reloaded = await read.Operators.SingleAsync(o => o.Id == created.Id);

        Assert.Equal(
            PasswordVerificationResult.Success,
            OperatorPasswordHasher.VerifyHashedPassword(reloaded, reloaded.PasswordHash, plaintext));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            OperatorPasswordHasher.VerifyHashedPassword(reloaded, reloaded.PasswordHash, "a-different-password"));
    }

    [Theory]
    [InlineData(OperatorRole.Administrator, OperatorConfiguration.AdministratorRoleToken)]
    [InlineData(OperatorRole.Teller, OperatorConfiguration.TellerRoleToken)]
    [InlineData(OperatorRole.Viewer, OperatorConfiguration.ViewerRoleToken)]
    public async Task EachFixedRolePersistsAsItsDocumentedLowercaseToken(OperatorRole role, string expectedToken)
    {
        await using BankDbContext context = await CreateMigratedContextAsync();
        Operator created = OperatorFactory.Create(FixedTimeProvider, $"role-token-{expectedToken}", "P@ssw0rd!12345", role);
        context.Operators.Add(created);
        await context.SaveChangesAsync();

        string? persistedToken = await ExecuteScalarAsync<string>(
            $"SELECT \"Role\" FROM \"{OperatorConfiguration.TableName}\" WHERE \"Id\" = @id;",
            ("id", created.Id));

        Assert.Equal(expectedToken, persistedToken);
    }

    [Theory]
    [InlineData(OperatorState.Active, OperatorConfiguration.ActiveStateToken)]
    [InlineData(OperatorState.Disabled, OperatorConfiguration.DisabledStateToken)]
    public async Task EachStatePersistsAsItsDocumentedLowercaseToken(OperatorState state, string expectedToken)
    {
        await using BankDbContext context = await CreateMigratedContextAsync();
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, $"state-token-{expectedToken}", "P@ssw0rd!12345", OperatorRole.Viewer, state);
        context.Operators.Add(created);
        await context.SaveChangesAsync();

        string? persistedToken = await ExecuteScalarAsync<string>(
            $"SELECT \"State\" FROM \"{OperatorConfiguration.TableName}\" WHERE \"Id\" = @id;",
            ("id", created.Id));

        Assert.Equal(expectedToken, persistedToken);
    }

    [Fact]
    public async Task PersistingAnUnspecifiedRoleThroughTheNormalEfPathIsRejectedByTheRoleCheckConstraint()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();
        Operator created = OperatorFactory.Create(
            FixedTimeProvider, "zero-role-user", "P@ssw0rd!12345", OperatorRole.Unspecified);
        context.Operators.Add(created);

        PostgresException failure = await ExpectPostgresExceptionAsync(() => context.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(OperatorConfiguration.RoleCheckConstraintName, failure.ConstraintName);
    }

    [Fact]
    public async Task PersistingANullRoleColumnDirectlyIsRejectedByNotNullRegardlessOfTheCheckConstraint()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();

        PostgresException failure = await ExpectPostgresExceptionAsync(() => ExecuteNonQueryAsync(
            $"""
             INSERT INTO "{OperatorConfiguration.TableName}"
                 ("Id", "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "Role", "State", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt")
             VALUES
                 (gen_random_uuid(), 'null-role-user', 'NULL-ROLE-USER', 'hash', 'stamp', NULL, 'active', 1, now(), now());
             """));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, failure.SqlState);
    }

    [Fact]
    public async Task PersistingAnUnrecognizedRoleTokenIsRejectedByTheRoleCheckConstraint()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();

        // No token can ever encode more than one simultaneous role: an attempt to represent
        // "administrator and teller" in the single scalar column is just another unrecognized
        // string, rejected the same way a typo would be.
        PostgresException failure = await ExpectPostgresExceptionAsync(() => ExecuteNonQueryAsync(
            $"""
             INSERT INTO "{OperatorConfiguration.TableName}"
                 ("Id", "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "Role", "State", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt")
             VALUES
                 (gen_random_uuid(), 'multi-role-user', 'MULTI-ROLE-USER', 'hash', 'stamp', 'administrator,teller', 'active', 1, now(), now());
             """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(OperatorConfiguration.RoleCheckConstraintName, failure.ConstraintName);
    }

    [Fact]
    public async Task PersistingAnInvalidStateTokenIsRejectedByTheStateCheckConstraint()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();

        PostgresException failure = await ExpectPostgresExceptionAsync(() => ExecuteNonQueryAsync(
            $"""
             INSERT INTO "{OperatorConfiguration.TableName}"
                 ("Id", "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "Role", "State", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt")
             VALUES
                 (gen_random_uuid(), 'invalid-state-user', 'INVALID-STATE-USER', 'hash', 'stamp', 'administrator', 'closed', 1, now(), now());
             """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(OperatorConfiguration.StateCheckConstraintName, failure.ConstraintName);
    }

    [Fact]
    public async Task NormalizedUserNameUniquenessIsCaseInsensitive()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();
        context.Operators.Add(OperatorFactory.Create(FixedTimeProvider, "Alice", "P@ssw0rd!12345", OperatorRole.Viewer));
        await context.SaveChangesAsync();

        await using BankDbContext second = await CreateMigratedContextAsync();
        second.Operators.Add(OperatorFactory.Create(FixedTimeProvider, "ALICE", "P@ssw0rd!67890", OperatorRole.Teller));

        PostgresException failure = await ExpectPostgresExceptionAsync(() => second.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(OperatorConfiguration.NormalizedUserNameIndexName, failure.ConstraintName);
    }

    [Fact]
    public async Task TheDatabaseNeverSuppliesAnOperatorIdWhenOneIsOmitted()
    {
        await using BankDbContext context = await CreateMigratedContextAsync();

        PostgresException failure = await ExpectPostgresExceptionAsync(() => ExecuteNonQueryAsync(
            $"""
             INSERT INTO "{OperatorConfiguration.TableName}"
                 ("UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "Role", "State", "AuthorizationStateVersion", "CreatedAt", "UpdatedAt")
             VALUES
                 ('no-id-user', 'NO-ID-USER', 'hash', 'stamp', 'viewer', 'active', 1, now(), now());
             """));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, failure.SqlState);
    }

    private async Task<BankDbContext> CreateMigratedContextAsync()
    {
        DbContextOptionsBuilder<BankDbContext> builder = new();
        builder.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        BankDbContext context = new(builder.Options);
        await context.Database.MigrateAsync();
        return context;
    }

    private async Task<T?> ExecuteScalarAsync<T>(string commandText, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);

        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        object? result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<PostgresException> ExpectPostgresExceptionAsync(Func<Task> action)
    {
        Exception thrown = await Record.ExceptionAsync(action) ??
            throw new InvalidOperationException("Expected the action to throw, but it completed successfully.");

        for (Exception? candidate = thrown; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        throw new InvalidOperationException(
            $"Expected a PostgresException somewhere in the exception chain, but got: {thrown}.");
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
