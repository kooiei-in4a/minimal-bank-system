using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Auditing;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuditPersistenceTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SuccessOperation = "verification.audit.success";
    private const string FailureOperation = "verification.audit.failure";
    private const string StateChangeOperation = "verification.audit.state-change";
    private const string QueryOperation = "verification.audit.query";
    private const string TransactionFixture = "audit_transaction_verification_fixture";

    private static readonly DateTimeOffset FrozenUtc =
        new(2034, 6, 7, 8, 9, 10, TimeSpan.Zero);

    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task AllLogicalFieldsRoundTripAndActorSnapshotSurvivesOperatorRoleChange()
    {
        await MigrateAsync();
        Guid actorId = await SeedOperatorAsync(OperatorRole.Teller);
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName);

        await using BankDbContext context = CreateContext(runtime.ConnectionString);
        AuditTransactionRunner runner = CreateRunner(
            context,
            [SuccessOperation, FailureOperation],
            new FrozenTimeProvider(FrozenUtc));

        AuditWriteRequest success = AuditWriteRequest.Success(
            actorId,
            OperatorRole.Teller,
            SuccessOperation,
            "account:000000000123",
            "audit-positive-success");
        AuditWriteRequest failure = AuditWriteRequest.Failure(
            actorId,
            OperatorRole.Teller,
            FailureOperation,
            $"operator:{actorId:D}",
            "operation_rejected",
            "audit-positive-failure");

        await runner.AppendInSeparateShortTransactionAsync(success);
        await runner.AppendInSeparateShortTransactionAsync(failure);

        AuditRecord[] records = await context.AuditRecords
            .AsNoTracking()
            .OrderBy(record => record.OperationIdentifier)
            .ToArrayAsync();
        Assert.Equal(2, records.Length);

        AuditRecord failed = Assert.Single(records, record => record.Result == AuditResult.Failure);
        Assert.Equal(actorId, failed.ActorIdentifier);
        Assert.Equal(OperatorRole.Teller, failed.ActorRole);
        Assert.Equal(FailureOperation, failed.OperationIdentifier);
        Assert.Equal($"operator:{actorId:D}", failed.TargetIdentifier);
        Assert.Equal("operation_rejected", failed.FailureBusinessErrorCode);
        Assert.Equal("audit-positive-failure", failed.CorrelationId);
        Assert.Equal(FrozenUtc, failed.AuditTime);
        Assert.Equal(7, failed.AuditId.Version);

        AuditRecord succeeded = Assert.Single(records, record => record.Result == AuditResult.Success);
        Assert.Equal(actorId, succeeded.ActorIdentifier);
        Assert.Equal(OperatorRole.Teller, succeeded.ActorRole);
        Assert.Equal(SuccessOperation, succeeded.OperationIdentifier);
        Assert.Equal("account:000000000123", succeeded.TargetIdentifier);
        Assert.Null(succeeded.FailureBusinessErrorCode);
        Assert.Equal("audit-positive-success", succeeded.CorrelationId);
        Assert.Equal(FrozenUtc, succeeded.AuditTime);
        Assert.Equal(7, succeeded.AuditId.Version);

        await ExecuteNonQueryAsync(
            $"UPDATE {OperatorPersistence.TableName} SET {OperatorPersistence.FixedRoleColumn} = 'viewer' " +
            $"WHERE {OperatorPersistence.IdColumn} = @actorId;",
            ("actorId", actorId));

        context.ChangeTracker.Clear();
        Assert.All(
            await context.AuditRecords.AsNoTracking().ToArrayAsync(),
            record => Assert.Equal(OperatorRole.Teller, record.ActorRole));

        string storedType = await ExecuteScalarAsync<string>(
            $"SELECT data_type FROM information_schema.columns WHERE table_schema = 'public' " +
            $"AND table_name = '{AuditPersistence.TableName}' " +
            $"AND column_name = '{AuditPersistence.AuditTimeColumn}';");
        Assert.Equal("timestamp with time zone", storedType);
    }

    [Fact]
    public async Task SensitiveInputExistsButOnlyTheApprovedFieldSetCanReachProductAudit()
    {
        await MigrateAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName);

        SensitiveVerificationEnvelope source = new(
            ActorIdentifier: Guid.CreateVersion7(FrozenUtc),
            ActorRole: OperatorRole.Administrator,
            OperationIdentifier: SuccessOperation,
            TargetIdentifier: "operator:018f0000-0000-7000-8000-000000000001",
            CorrelationId: "sensitive-negative-non-vacuous",
            Credential: "CREDENTIAL_SENTINEL_AUD01",
            BearerJwt: "JWT_SENTINEL_AUD01",
            UnnecessaryPersonalInformation: "PERSONAL_SENTINEL_AUD01");

        await using BankDbContext context = CreateContext(runtime.ConnectionString);
        AuditTransactionRunner runner = CreateRunner(
            context,
            [SuccessOperation],
            new FrozenTimeProvider(FrozenUtc));

        // The mapping is intentionally explicit: the source really contains prohibited values,
        // while the product request type has no property capable of carrying them.
        await runner.AppendInSeparateShortTransactionAsync(
            AuditWriteRequest.Success(
                source.ActorIdentifier,
                source.ActorRole,
                source.OperationIdentifier,
                source.TargetIdentifier,
                source.CorrelationId));

        string[] expectedColumns =
        [
            AuditPersistence.ActorIdentifierColumn,
            AuditPersistence.ActorRoleColumn,
            AuditPersistence.AuditIdColumn,
            AuditPersistence.AuditTimeColumn,
            AuditPersistence.CorrelationIdColumn,
            AuditPersistence.FailureBusinessErrorCodeColumn,
            AuditPersistence.OperationIdentifierColumn,
            AuditPersistence.ResultColumn,
            AuditPersistence.TargetIdentifierColumn,
        ];
        Assert.Equal(expectedColumns, await ReadStringsAsync(
            $"SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' " +
            $"AND table_name = '{AuditPersistence.TableName}' ORDER BY column_name;"));

        string persistedText = await ExecuteScalarAsync<string>(
            $"SELECT concat_ws('|', {AuditPersistence.AuditIdColumn}::text, " +
            $"{AuditPersistence.ActorIdentifierColumn}::text, {AuditPersistence.ActorRoleColumn}, " +
            $"{AuditPersistence.OperationIdentifierColumn}, {AuditPersistence.TargetIdentifierColumn}, " +
            $"{AuditPersistence.ResultColumn}, {AuditPersistence.FailureBusinessErrorCodeColumn}, " +
            $"{AuditPersistence.CorrelationIdColumn}, {AuditPersistence.AuditTimeColumn}::text) " +
            $"FROM {AuditPersistence.TableName};");
        Assert.DoesNotContain(source.Credential, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(source.BearerJwt, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(source.UnnecessaryPersonalInformation, persistedText, StringComparison.Ordinal);

        string[] forbiddenNames = ["credential", "secret", "token", "jwt", "password", "payload", "personal"];
        Assert.All(
            typeof(AuditWriteRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => Assert.DoesNotContain(
                forbiddenNames,
                forbidden => property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
        Assert.All(
            typeof(AuditRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => Assert.DoesNotContain(
                forbiddenNames,
                forbidden => property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ApiEquivalentRuntimePrincipalAppendsButCannotUpdateOrDeleteHistory()
    {
        await MigrateAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName);
        await using BankDbContext context = CreateContext(runtime.ConnectionString);
        AuditTransactionRunner runner = CreateRunner(
            context,
            [SuccessOperation],
            new FrozenTimeProvider(FrozenUtc));

        await runner.AppendInSeparateShortTransactionAsync(
            AuditWriteRequest.Success(
                Guid.CreateVersion7(FrozenUtc),
                OperatorRole.Viewer,
                SuccessOperation,
                "account:000000000321",
                "runtime-principal-append"));

        PostgresException update = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteNonQueryAsync(
                runtime.ConnectionString,
                $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'account:mutated';"));
        Assert.Equal("55000", update.SqlState);
        Assert.Equal("Product Audit history is append-only.", update.MessageText);

        PostgresException delete = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteNonQueryAsync(runtime.ConnectionString, $"DELETE FROM {AuditPersistence.TableName};"));
        Assert.Equal("55000", delete.SqlState);
        Assert.Equal("Product Audit history is append-only.", delete.MessageText);
        Assert.Equal(1L, await ExecuteScalarAsync<long>($"SELECT count(*) FROM {AuditPersistence.TableName};"));
    }

    [Fact]
    public async Task CallerTransactionCommitRollbackAndAuditFailureHaveBidirectionalDatabaseOracles()
    {
        await MigrateAsync();
        await CreateTransactionFixtureAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName,
            TransactionFixture);

        Guid committedId = Guid.CreateVersion7(FrozenUtc);
        await using (BankDbContext context = CreateContext(runtime.ConnectionString))
        {
            AuditTransactionRunner runner = CreateRunner(
                context,
                [StateChangeOperation],
                new FrozenTimeProvider(FrozenUtc));
            Guid result = await runner.ExecuteStateChangingAsync(
                async (callerContext, cancellationToken) =>
                {
                    await callerContext.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO audit_transaction_verification_fixture (id, note) VALUES ({committedId}, {"committed"});",
                        cancellationToken);
                    return committedId;
                },
                id => AuditWriteRequest.Success(
                    Guid.CreateVersion7(FrozenUtc),
                    OperatorRole.Teller,
                    StateChangeOperation,
                    $"probe:{id:D}",
                    "caller-transaction-commit"));
            Assert.Equal(committedId, result);
        }

        Assert.Equal(1L, await CountFixtureAsync(committedId));
        Assert.Equal(1L, await CountAuditAsync("caller-transaction-commit"));

        Guid rolledBackId = Guid.CreateVersion7(FrozenUtc.AddSeconds(1));
        await using (BankDbContext context = CreateContext(runtime.ConnectionString))
        await using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted))
        {
            AuditWriter writer = CreateWriter(
                context,
                [StateChangeOperation],
                new FrozenTimeProvider(FrozenUtc.AddSeconds(1)));
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO audit_transaction_verification_fixture (id, note) VALUES ({rolledBackId}, {"rolled-back"});");
            await writer.AppendInCallerTransactionAsync(
                AuditWriteRequest.Success(
                    Guid.CreateVersion7(FrozenUtc),
                    OperatorRole.Teller,
                    StateChangeOperation,
                    $"probe:{rolledBackId:D}",
                    "caller-transaction-rollback"));
            await transaction.RollbackAsync();
        }

        Assert.Equal(0L, await CountFixtureAsync(rolledBackId));
        Assert.Equal(0L, await CountAuditAsync("caller-transaction-rollback"));

        Guid failedId = Guid.CreateVersion7(FrozenUtc.AddSeconds(2));
        AuditFailureInterceptor failure = new();
        await using (BankDbContext context = CreateContext(runtime.ConnectionString, failure))
        {
            AuditTransactionRunner runner = CreateRunner(
                context,
                [StateChangeOperation],
                new FrozenTimeProvider(FrozenUtc.AddSeconds(2)));
            AuditFailureInjectionException exception = await Assert.ThrowsAsync<AuditFailureInjectionException>(() =>
                runner.ExecuteStateChangingAsync(
                    async (callerContext, cancellationToken) =>
                    {
                        await callerContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO audit_transaction_verification_fixture (id, note) VALUES ({failedId}, {"must-not-commit"});",
                            cancellationToken);
                        return failedId;
                    },
                    id => AuditWriteRequest.Success(
                        Guid.CreateVersion7(FrozenUtc),
                        OperatorRole.Teller,
                        StateChangeOperation,
                        $"probe:{id:D}",
                        "caller-transaction-audit-failure")));
            Assert.Equal(AuditFailureInjectionException.SemanticSignature, exception.Message);
            Assert.True(failure.AuditSaveWasReached);
        }

        Assert.Equal(0L, await CountFixtureAsync(failedId));
        Assert.Equal(0L, await CountAuditAsync("caller-transaction-audit-failure"));
    }

    [Fact]
    public async Task UnregisteredOperationFailsClosedAfterCallerMutationButBeforeAnyCommit()
    {
        await MigrateAsync();
        await CreateTransactionFixtureAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName,
            TransactionFixture);
        Guid probeId = Guid.CreateVersion7(FrozenUtc);

        await using BankDbContext context = CreateContext(runtime.ConnectionString);
        AuditTransactionRunner runner = CreateRunner(
            context,
            [StateChangeOperation],
            new FrozenTimeProvider(FrozenUtc));

        UnregisteredAuditOperationException exception =
            await Assert.ThrowsAsync<UnregisteredAuditOperationException>(() =>
                runner.ExecuteStateChangingAsync(
                    async (callerContext, cancellationToken) =>
                    {
                        await callerContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO audit_transaction_verification_fixture (id, note) VALUES ({probeId}, {"unregistered"});",
                            cancellationToken);
                        return probeId;
                    },
                    id => AuditWriteRequest.Success(
                        Guid.CreateVersion7(FrozenUtc),
                        OperatorRole.Administrator,
                        "not.registered.operation",
                        $"probe:{id:D}",
                        "unregistered-operation")));

        Assert.Contains("not.registered.operation", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0L, await CountFixtureAsync(probeId));
        Assert.Equal(0L, await ExecuteScalarAsync<long>($"SELECT count(*) FROM {AuditPersistence.TableName};"));
    }

    [Fact]
    public async Task SeparateShortTransactionCommitsBeforeReturnAndFailureReturnsNoPayload()
    {
        await MigrateAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName);

        bool queryExecuted = false;
        await using (BankDbContext context = CreateContext(runtime.ConnectionString))
        {
            AuditTransactionRunner runner = CreateRunner(
                context,
                [QueryOperation],
                new FrozenTimeProvider(FrozenUtc));
            string returnedPayload = await runner.ExecuteAuditedQueryAsync(
                _ =>
                {
                    queryExecuted = true;
                    return Task.FromResult("approved-success-payload");
                },
                _ => AuditWriteRequest.Success(
                    Guid.CreateVersion7(FrozenUtc),
                    OperatorRole.Viewer,
                    QueryOperation,
                    "account:000000000999",
                    "query-commit-before-return"));

            Assert.True(queryExecuted);
            Assert.Equal("approved-success-payload", returnedPayload);
            // A separate connection observes the durable row immediately after the payload becomes
            // caller-visible, proving the runner did not return before COMMIT.
            Assert.Equal(1L, await CountAuditAsync("query-commit-before-return"));
        }

        queryExecuted = false;
        string? exposedPayload = null;
        AuditFailureInterceptor failure = new();
        await using (BankDbContext context = CreateContext(runtime.ConnectionString, failure))
        {
            AuditTransactionRunner runner = CreateRunner(
                context,
                [QueryOperation],
                new FrozenTimeProvider(FrozenUtc.AddSeconds(1)));

            AuditFailureInjectionException exception = await Assert.ThrowsAsync<AuditFailureInjectionException>(async () =>
                exposedPayload = await runner.ExecuteAuditedQueryAsync(
                    _ =>
                    {
                        queryExecuted = true;
                        return Task.FromResult("must-not-be-returned");
                    },
                    _ => AuditWriteRequest.Success(
                        Guid.CreateVersion7(FrozenUtc),
                        OperatorRole.Viewer,
                        QueryOperation,
                        "account:000000000998",
                        "query-audit-failure")));
            Assert.Equal(AuditFailureInjectionException.SemanticSignature, exception.Message);
            Assert.True(failure.AuditSaveWasReached);
        }

        Assert.True(queryExecuted);
        Assert.Null(exposedPayload);
        Assert.Equal(0L, await CountAuditAsync("query-audit-failure"));
    }

    [Fact]
    public async Task AuditDownFailsClosedAtTheBackupRestoreBoundaryWithoutLosingHistory()
    {
        await MigrateAsync();
        await using AuditRuntimePrincipal runtime = await AuditRuntimePrincipal.CreateAsync(
            Database.ConnectionString,
            AuditPersistence.TableName);
        await using (BankDbContext runtimeContext = CreateContext(runtime.ConnectionString))
        {
            AuditTransactionRunner runner = CreateRunner(
                runtimeContext,
                [SuccessOperation],
                new FrozenTimeProvider(FrozenUtc));
            await runner.AppendInSeparateShortTransactionAsync(
                AuditWriteRequest.Success(
                    Guid.CreateVersion7(FrozenUtc),
                    OperatorRole.Administrator,
                    SuccessOperation,
                    "operator:018f0000-0000-7000-8000-000000000002",
                    "down-boundary-preserves-audit"));
        }

        await using BankDbContext migratorContext = CreateContext(Database.ConnectionString);
        Exception migrationFailure = await Assert.ThrowsAnyAsync<Exception>(() =>
            migratorContext.GetService<IMigrator>().MigrateAsync(OperatorPersistence.IdentityMigrationId));
        PostgresException down = Assert.IsType<PostgresException>(migrationFailure.GetBaseException());
        Assert.Equal("55000", down.SqlState);
        Assert.Equal(AuditPersistence.RollbackRequiresBackupRestoreSignature, down.MessageText);

        Assert.Equal(1L, await CountAuditAsync("down-boundary-preserves-audit"));
        Assert.Equal(
            AuditPersistence.AuditMigrationId,
            await ExecuteScalarAsync<string>(
                $"SELECT \"MigrationId\" FROM public.\"{BankPersistence.MigrationsHistoryTableName}\" " +
                "ORDER BY \"MigrationId\" DESC LIMIT 1;"));
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, MigrationBudget);
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Audit migration failed. Output:\n{run.Output}");
    }

    private async Task<Guid> SeedOperatorAsync(OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            "audit.snapshot.teller",
            "audit-snapshot-password-not-for-production",
            role,
            FrozenUtc,
            Guid.NewGuid().ToString());
        await using BankDbContext context = CreateContext(Database.ConnectionString);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created.Id;
    }

    private async Task CreateTransactionFixtureAsync()
    {
        await ExecuteNonQueryAsync(
            $"CREATE TABLE {TransactionFixture} (id uuid PRIMARY KEY, note text NOT NULL);");
    }

    private Task<long> CountFixtureAsync(Guid id) =>
        ExecuteScalarAsync<long>($"SELECT count(*) FROM {TransactionFixture} WHERE id = @id;", ("id", id));

    private Task<long> CountAuditAsync(string correlationId) =>
        ExecuteScalarAsync<long>(
            $"SELECT count(*) FROM {AuditPersistence.TableName} " +
            $"WHERE {AuditPersistence.CorrelationIdColumn} = @correlationId;",
            ("correlationId", correlationId));

    private static AuditWriter CreateWriter(
        BankDbContext context,
        string[] operations,
        TimeProvider timeProvider) =>
        new(context, new AuditOperationRegistry(operations), timeProvider);

    private static AuditTransactionRunner CreateRunner(
        BankDbContext context,
        string[] operations,
        TimeProvider timeProvider)
    {
        AuditWriter writer = CreateWriter(context, operations, timeProvider);
        return new AuditTransactionRunner(context, writer);
    }

    private static BankDbContext CreateContext(
        string connectionString,
        SaveChangesInterceptor? interceptor = null)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(connectionString, BankPersistence.MigrationTimeoutSeconds);

        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new BankDbContext(options.Options);
    }

    private Task ExecuteNonQueryAsync(
        string commandText,
        params (string Name, object Value)[] parameters) =>
        ExecuteNonQueryAsync(Database.ConnectionString, commandText, parameters);

    private static async Task ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private Task<T> ExecuteScalarAsync<T>(
        string commandText,
        params (string Name, object Value)[] parameters) =>
        ExecuteScalarAsync<T>(Database.ConnectionString, commandText, parameters);

    private static async Task<T> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Assert.IsType<T>(await command.ExecuteScalarAsync());
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

    private sealed record SensitiveVerificationEnvelope(
        Guid ActorIdentifier,
        OperatorRole ActorRole,
        string OperationIdentifier,
        string TargetIdentifier,
        string CorrelationId,
        string Credential,
        string BearerJwt,
        string UnnecessaryPersonalInformation);
}

internal sealed class AuditFailureInterceptor : SaveChangesInterceptor
{
    public bool AuditSaveWasReached { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.ChangeTracker.Entries<AuditRecord>()
            .Any(entry => entry.State == EntityState.Added) is true)
        {
            AuditSaveWasReached = true;
            throw new AuditFailureInjectionException();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

internal sealed class AuditFailureInjectionException()
    : Exception(AuditFailureInjectionException.SemanticSignature)
{
    public const string SemanticSignature = "TEST_ONLY_REQUIRED_AUDIT_PERSISTENCE_FAILURE";
}

internal sealed class AuditRuntimePrincipal : IAsyncDisposable
{
    private readonly string administratorConnectionString;
    private readonly string roleName;

    private AuditRuntimePrincipal(
        string administratorConnectionString,
        string roleName,
        string connectionString)
    {
        this.administratorConnectionString = administratorConnectionString;
        this.roleName = roleName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<AuditRuntimePrincipal> CreateAsync(
        string administratorConnectionString,
        params string[] grantedTables)
    {
        string roleName = $"mbs_audit_{Guid.NewGuid():N}";
        // PostgreSQL utility statements do not accept bind parameters for role
        // passwords. Hex encoding keeps this test-only literal in a fixed safe
        // alphabet while preserving a fresh cryptographically-random secret.
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        NpgsqlConnectionStringBuilder runtime = new(administratorConnectionString)
        {
            Username = roleName,
            Password = password,
            Pooling = false,
        };

        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"CREATE ROLE \"{roleName}\" LOGIN PASSWORD '{password}' " +
            "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; " +
            $"GRANT CONNECT ON DATABASE \"{runtime.Database}\" TO \"{roleName}\"; " +
            $"GRANT USAGE ON SCHEMA public TO \"{roleName}\"; " +
            string.Join(
                ' ',
                grantedTables.Select(table =>
                    $"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.\"{table}\" TO \"{roleName}\";")),
            connection);
        await command.ExecuteNonQueryAsync();

        return new AuditRuntimePrincipal(
            administratorConnectionString,
            roleName,
            runtime.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"DROP OWNED BY \"{roleName}\"; DROP ROLE \"{roleName}\";",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
