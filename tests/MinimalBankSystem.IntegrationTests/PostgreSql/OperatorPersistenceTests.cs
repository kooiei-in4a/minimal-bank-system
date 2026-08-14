using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorPersistenceTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    internal const string SeedPlaintextPassword = "ID01-integration-seed-password-not-for-production";

    private static readonly DateTimeOffset FrozenUtc =
        new(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);

    [Theory]
    [InlineData(OperatorRole.Administrator, "administrator", "admin")]
    [InlineData(OperatorRole.Teller, "teller", "teller")]
    [InlineData(OperatorRole.Viewer, "viewer", "viewer")]
    public async Task OperatorRoundTripsUuidV7RoleStatePasswordAndAuthorizationFields(
        OperatorRole role,
        string expectedRoleToken,
        string userNameSuffix)
    {
        await MigrateAsync();

        ApplicationTime time = new(new FrozenTimeProvider(FrozenUtc));
        DateTimeOffset utcNow = time.GetUtcNow();
        string securityStamp = Guid.NewGuid().ToString();
        Operator created = Operator.Create(
            userName: $"id01.roundtrip.{userNameSuffix}",
            passwordHash: IdentityPassword.Hash(SeedPlaintextPassword),
            role,
            utcNow: utcNow,
            securityStamp: securityStamp);

        Assert.Equal(7, created.Id.Version);
        Assert.Equal(utcNow, created.CreatedAt);
        Assert.Equal(utcNow, created.UpdatedAt);

        await using (BankDbContext writeContext = CreateContext())
        {
            writeContext.Operators.Add(created);
            await writeContext.SaveChangesAsync();
        }

        await using BankDbContext readContext = CreateContext();
        Operator loaded = await readContext.Operators.SingleAsync(candidate => candidate.Id == created.Id);

        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(7, loaded.Id.Version);
        Assert.Equal($"id01.roundtrip.{userNameSuffix}", loaded.UserName);
        Assert.Equal($"ID01.ROUNDTRIP.{userNameSuffix.ToUpperInvariant()}", loaded.NormalizedUserName);
        Assert.Equal(OperatorState.Active, loaded.State);
        Assert.Equal(role, loaded.Role);
        Assert.Equal(Operator.InitialAuthorizationStateVersion, loaded.AuthorizationStateVersion);
        Assert.Equal(securityStamp, loaded.SecurityStamp);
        Assert.Equal(FrozenUtc, loaded.CreatedAt);
        Assert.Equal(FrozenUtc, loaded.UpdatedAt);
        Assert.NotEqual(SeedPlaintextPassword, loaded.PasswordHash);
        Assert.DoesNotContain(SeedPlaintextPassword, loaded.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, IdentityPassword.Verify(loaded, SeedPlaintextPassword));
        Assert.Equal(PasswordVerificationResult.Failed, IdentityPassword.Verify(loaded, "incorrect-password"));

        string storedRole = Assert.IsType<string>(
            await ExecuteScalarAsync($"SELECT {OperatorPersistence.FixedRoleColumn} FROM {OperatorPersistence.TableName};"));
        Assert.Equal(expectedRoleToken, storedRole);

        string storedState = Assert.IsType<string>(
            await ExecuteScalarAsync($"SELECT {OperatorPersistence.StateColumn} FROM {OperatorPersistence.TableName};"));
        Assert.Equal(OperatorPersistence.ActiveStateToken, storedState);

        string storedHash = Assert.IsType<string>(
            await ExecuteScalarAsync($"SELECT {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName};"));
        Assert.NotEqual(SeedPlaintextPassword, storedHash);
        Assert.DoesNotContain(SeedPlaintextPassword, storedHash, StringComparison.Ordinal);

        string storedType = Assert.IsType<string>(
            await ExecuteScalarAsync(
                $"""
                 SELECT data_type
                 FROM information_schema.columns
                 WHERE table_schema = 'public'
                   AND table_name = '{OperatorPersistence.TableName}'
                   AND column_name = '{OperatorPersistence.CreatedAtColumn}';
                 """));
        Assert.Equal("timestamp with time zone", storedType);
    }

    [Fact]
    public async Task DatabaseRejectsZeroCurrentRolesAndMultipleCurrentRoles()
    {
        await MigrateAsync();

        PostgresException missingRole = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(currentRoleSqlLiteral: "NULL"));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, missingRole.SqlState);

        PostgresException multipleRoles = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(currentRoleSqlLiteral: "'administrator,teller'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, multipleRoles.SqlState);

        PostgresException unknownRole = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(currentRoleSqlLiteral: "'supervisor'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, unknownRole.SqlState);

        Assert.Empty(await ReadStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('AspNetRoles', 'AspNetUserRoles', 'AspNetUsers', 'operator_roles');
            """));
    }

    [Fact]
    public async Task DatabaseRejectsUnknownOperatorState()
    {
        await MigrateAsync();

        PostgresException invalidState = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(stateSqlLiteral: "'locked'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidState.SqlState);
    }

    [Fact]
    public async Task IdentityRowsSurvivePreviousToLatestUpgrade()
    {
        await using (BankDbContext context = CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync("20260809113338_InitialFoundation");
        }

        MigratorRun upgrade = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, upgrade.ExitCode);

        Operator created = CreateSeededOperator("id01.upgrade.viewer", OperatorRole.Viewer);
        await using (BankDbContext writeContext = CreateContext())
        {
            writeContext.Operators.Add(created);
            await writeContext.SaveChangesAsync();
        }

        MigratorRun rerun = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, rerun.ExitCode);

        await using BankDbContext readContext = CreateContext();
        Operator loaded = await readContext.Operators.SingleAsync();
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(OperatorRole.Viewer, loaded.Role);
        Assert.Equal(created.AuthorizationStateVersion, loaded.AuthorizationStateVersion);
        Assert.Equal(created.SecurityStamp, loaded.SecurityStamp);
    }

    [Fact]
    public async Task IntegrationSeedIsUnreachableFromProductionRuntime()
    {
        await MigrateAsync();

        Assert.DoesNotContain(
            SeedPlaintextPassword,
            string.Join(
                '\n',
                Directory.EnumerateFiles(
                    Path.Combine(RepositoryLayout.RepositoryRoot.FullName, "src"),
                    "*.cs",
                    SearchOption.AllDirectories)
                    .Select(File.ReadAllText)),
            StringComparison.Ordinal);

        await using HealthApiFactory factory = new(Database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
        Assert.NotNull(response);

        using IServiceScope scope = factory.Services.CreateScope();
        BankDbContext context = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        Assert.Empty(await context.Operators.ToListAsync());
        Assert.Null(scope.ServiceProvider.GetService<IPasswordHasher<Operator>>());
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private static Operator CreateSeededOperator(string userName, OperatorRole role) =>
        Operator.Create(
            userName,
            IdentityPassword.Hash(SeedPlaintextPassword),
            role,
            FrozenUtc,
            Guid.NewGuid().ToString());

    private async Task InsertRawAsync(
        string currentRoleSqlLiteral = "'administrator'",
        string stateSqlLiteral = "'active'")
    {
        Guid id = Guid.CreateVersion7(FrozenUtc);
        await ExecuteNonQueryAsync(
            $"""
             INSERT INTO {OperatorPersistence.TableName} (
                 {OperatorPersistence.IdColumn},
                 {OperatorPersistence.UserNameColumn},
                 {OperatorPersistence.NormalizedUserNameColumn},
                 {OperatorPersistence.PasswordHashColumn},
                 {OperatorPersistence.SecurityStampColumn},
                 {OperatorPersistence.StateColumn},
                 {OperatorPersistence.FixedRoleColumn},
                 {OperatorPersistence.AuthorizationStateVersionColumn},
                 {OperatorPersistence.CreatedAtColumn},
                 {OperatorPersistence.UpdatedAtColumn}
             ) VALUES (
                 '{id}',
                 'raw-{id:N}',
                 'RAW-{id:N}',
                 'not-a-plaintext-hash',
                 'stamp',
                 {stateSqlLiteral},
                 {currentRoleSqlLiteral},
                 1,
                 TIMESTAMPTZ '2031-02-03 04:05:06+00',
                 TIMESTAMPTZ '2031-02-03 04:05:06+00'
             );
             """);
    }

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ExecuteScalarAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        return await command.ExecuteScalarAsync();
    }

    private async Task<string[]> ReadStringsAsync(string commandText)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> values = [];
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }
}

internal sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
