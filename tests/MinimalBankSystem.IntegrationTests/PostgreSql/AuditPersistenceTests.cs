using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;
using Xunit.Abstractions;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuditPersistenceTests(
    PostgreSqlContainerFixture fixture,
    ITestOutputHelper output)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string RegisteredOperation = "verification.audit.operation";
    private const string UnknownOperation = "verification.audit.unregistered";
    internal const string CallerFixtureTable = "audit_verification_caller_state";
    private const string PersonalDataSentinel = "UNNECESSARY_PERSONAL_DATA_SENTINEL";
    private const string CredentialSentinel = "CREDENTIAL_SENTINEL";

    private static readonly DateTimeOffset FrozenLocalTime =
        new(2033, 4, 5, 15, 7, 8, TimeSpan.FromHours(9));

    [Fact]
    public async Task LogicalFieldsRoundTripAsUuidV7UtcStableSnapshotsWithoutSensitiveData()
    {
        await using AuditRuntimeBoundary boundary = await AuditRuntimeBoundary.CreateAsync(Database.ConnectionString);
        await boundary.MigrateAsync();

        Guid actorIdentifier = Guid.NewGuid();
        await boundary.ExecuteRuntimeNonQueryAsync(
            $"""
             INSERT INTO {OperatorPersistence.TableName} (
                 {OperatorPersistence.IdColumn}, {OperatorPersistence.UserNameColumn},
                 {OperatorPersistence.NormalizedUserNameColumn}, {OperatorPersistence.PasswordHashColumn},
                 {OperatorPersistence.SecurityStampColumn}, {OperatorPersistence.StateColumn},
                 {OperatorPersistence.FixedRoleColumn}, {OperatorPersistence.AuthorizationStateVersionColumn},
                 {OperatorPersistence.CreatedAtColumn}, {OperatorPersistence.UpdatedAtColumn}
             ) VALUES (
                 '{actorIdentifier}', '{PersonalDataSentinel}', '{PersonalDataSentinel}',
                 '{CredentialSentinel}', 'security-stamp', 'active', 'administrator', 1,
                 TIMESTAMPTZ '2033-04-05 06:07:08+00', TIMESTAMPTZ '2033-04-05 06:07:08+00'
             );
             """);

        const string targetIdentifier = "operator:verification-target";
        const string errorCode = "verification_rejected";
        const string correlationId = "audit-fields-correlation";

        await using (BankDbContext context = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(context);
            string payload = await writer.AppendInSeparateTransactionBeforeResultAsync(
                FailureRequest(actorIdentifier, targetIdentifier, correlationId, errorCode),
                _ => Task.FromResult("committed-success-payload"));

            Assert.Equal("committed-success-payload", payload);
        }

        await boundary.ExecuteRuntimeNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.FixedRoleColumn} = 'viewer',
                 {OperatorPersistence.StateColumn} = 'disabled'
             WHERE {OperatorPersistence.IdColumn} = '{actorIdentifier}';
             """);

        await using (BankDbContext readContext = boundary.CreateRuntimeContext())
        {
            AuditRecord record = await readContext.AuditRecords.AsNoTracking().SingleAsync();

            Assert.Equal(7, record.Id.Version);
            Assert.Equal(actorIdentifier, record.ActorIdentifier);
            Assert.Equal(OperatorRole.Administrator, record.ActorRole);
            Assert.Equal(RegisteredOperation, record.OperationIdentifier);
            Assert.Equal(targetIdentifier, record.TargetIdentifier);
            Assert.Equal(AuditResult.Failure, record.Result);
            Assert.Equal(errorCode, record.FailureBusinessErrorCode);
            Assert.Equal(correlationId, record.CorrelationId);
            Assert.Equal(FrozenLocalTime.ToUniversalTime(), record.AuditTime);
            Assert.Equal(TimeSpan.Zero, record.AuditTime.Offset);
        }

        string operatorPositiveControl = Assert.IsType<string>(
            await boundary.ExecuteRuntimeScalarAsync(
                $"SELECT {OperatorPersistence.UserNameColumn} || ':' || {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName};"));
        Assert.Contains(PersonalDataSentinel, operatorPositiveControl, StringComparison.Ordinal);
        Assert.Contains(CredentialSentinel, operatorPositiveControl, StringComparison.Ordinal);

        string auditJson = Assert.IsType<string>(
            await boundary.ExecuteRuntimeScalarAsync(
                $"SELECT row_to_json(a)::text FROM {AuditPersistence.TableName} AS a;"));
        Assert.DoesNotContain(PersonalDataSentinel, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CredentialSentinel, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("password", auditJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearer", auditJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt", auditJson, StringComparison.OrdinalIgnoreCase);

        long actorForeignKeys = Convert.ToInt64(
            await boundary.ExecuteRuntimeScalarAsync(
                $"""
                 SELECT count(*)
                 FROM information_schema.table_constraints
                 WHERE table_schema = 'public'
                   AND table_name = '{AuditPersistence.TableName}'
                   AND constraint_type = 'FOREIGN KEY';
                 """),
            CultureInfo.InvariantCulture);
        Assert.Equal(0, actorForeignKeys);

        string auditTimeType = Assert.IsType<string>(
            await boundary.ExecuteRuntimeScalarAsync(
                $"""
                 SELECT data_type
                 FROM information_schema.columns
                 WHERE table_schema = 'public'
                   AND table_name = '{AuditPersistence.TableName}'
                   AND column_name = '{AuditPersistence.AuditTimeColumn}';
                 """));
        Assert.Equal("timestamp with time zone", auditTimeType);
    }

    [Fact]
    public async Task CallerAndSeparateTransactionPrimitivesHaveBidirectionalFailClosedOracles()
    {
        await using AuditRuntimeBoundary boundary = await AuditRuntimeBoundary.CreateAsync(Database.ConnectionString);
        await boundary.MigrateAsync();
        await boundary.CreateCallerFixtureAsync();
        Guid actor = Guid.NewGuid();

        await using (BankDbContext successContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(successContext);
            await using var transaction = await successContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await InsertCallerStateAsync(successContext, "same-success", "committed");
            await writer.AppendToCurrentTransactionAsync(
                SuccessRequest(actor, "same-success", "same-success-correlation"));
            await transaction.CommitAsync();
        }

        Assert.Equal(1L, await boundary.CountCallerStateAsync("same-success"));
        Assert.Equal(1L, await boundary.CountAuditAsync("same-success-correlation"));

        await using (BankDbContext rollbackContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(rollbackContext);
            await using var transaction = await rollbackContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await InsertCallerStateAsync(rollbackContext, "explicit-rollback", "must-disappear");
            await writer.AppendToCurrentTransactionAsync(
                SuccessRequest(actor, "explicit-rollback", "explicit-rollback-correlation"));
            await transaction.RollbackAsync();
        }

        Assert.Equal(0L, await boundary.CountCallerStateAsync("explicit-rollback"));
        Assert.Equal(0L, await boundary.CountAuditAsync("explicit-rollback-correlation"));

        bool partialSuccessExposed = false;
        await using (BankDbContext failureContext = boundary.CreateRuntimeContext(
                         new ThrowOnAuditSaveChangesInterceptor()))
        {
            PostgreSqlAuditWriter writer = CreateWriter(failureContext);
            await using var transaction = await failureContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await InsertCallerStateAsync(failureContext, "required-audit-failure", "must-disappear");

            await Assert.ThrowsAsync<AuditPersistenceInjectionException>(async () =>
            {
                await writer.AppendToCurrentTransactionAsync(
                    SuccessRequest(actor, "required-audit-failure", "required-failure-correlation"));
                await transaction.CommitAsync();
                partialSuccessExposed = true;
            });
        }

        Assert.False(partialSuccessExposed);
        Assert.Equal(0L, await boundary.CountCallerStateAsync("required-audit-failure"));
        Assert.Equal(0L, await boundary.CountAuditAsync("required-failure-correlation"));

        await using (BankDbContext unknownContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = new(
                unknownContext,
                AuditOperationRegistry.Empty,
                new ApplicationTime(new FrozenTimeProvider(FrozenLocalTime)));
            await using var transaction = await unknownContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await InsertCallerStateAsync(unknownContext, "unregistered", "must-disappear");

            await Assert.ThrowsAsync<UnregisteredAuditOperationException>(
                () => writer.AppendToCurrentTransactionAsync(
                    SuccessRequest(actor, "unregistered", "unregistered-correlation") with
                    {
                        OperationIdentifier = UnknownOperation,
                    }));
        }

        Assert.Equal(0L, await boundary.CountCallerStateAsync("unregistered"));
        Assert.Equal(0L, await boundary.CountAuditAsync("unregistered-correlation"));

        await using (BankDbContext isolationContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(isolationContext);
            await using var transaction = await isolationContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await InsertCallerStateAsync(isolationContext, "wrong-isolation", "must-disappear");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.AppendToCurrentTransactionAsync(
                    SuccessRequest(actor, "wrong-isolation", "wrong-isolation-correlation")));
        }

        Assert.Equal(0L, await boundary.CountCallerStateAsync("wrong-isolation"));
        Assert.Equal(0L, await boundary.CountAuditAsync("wrong-isolation-correlation"));

        await using (BankDbContext duplicateContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(duplicateContext);
            AuditWriteRequest duplicate = SuccessRequest(actor, "duplicate", "duplicate-correlation");
            await using var transaction = await duplicateContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await InsertCallerStateAsync(duplicateContext, "duplicate", "must-disappear");
            await writer.AppendToCurrentTransactionAsync(duplicate);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.AppendToCurrentTransactionAsync(duplicate));
        }

        Assert.Equal(0L, await boundary.CountCallerStateAsync("duplicate"));
        Assert.Equal(0L, await boundary.CountAuditAsync("duplicate-correlation"));

        bool resultFactoryObservedCommittedAudit = false;
        await using (BankDbContext separateContext = boundary.CreateRuntimeContext())
        {
            PostgreSqlAuditWriter writer = CreateWriter(separateContext);
            string payload = await writer.AppendInSeparateTransactionBeforeResultAsync(
                SuccessRequest(actor, "separate-success", "separate-success-correlation"),
                async _ =>
                {
                    resultFactoryObservedCommittedAudit =
                        await boundary.CountAuditAsync("separate-success-correlation") == 1;
                    return "observable-success-payload";
                });

            Assert.Equal("observable-success-payload", payload);
        }

        Assert.True(resultFactoryObservedCommittedAudit);

        bool failedResultFactoryInvoked = false;
        await using (BankDbContext separateFailureContext = boundary.CreateRuntimeContext(
                         new ThrowOnAuditSaveChangesInterceptor()))
        {
            PostgreSqlAuditWriter writer = CreateWriter(separateFailureContext);
            await Assert.ThrowsAsync<AuditPersistenceInjectionException>(
                () => writer.AppendInSeparateTransactionBeforeResultAsync(
                    SuccessRequest(actor, "separate-failure", "separate-failure-correlation"),
                    _ =>
                    {
                        failedResultFactoryInvoked = true;
                        return Task.FromResult("must-not-be-returned");
                    }));
        }

        Assert.False(failedResultFactoryInvoked);
        Assert.Equal(0L, await boundary.CountAuditAsync("separate-failure-correlation"));
    }

    [Fact]
    public async Task AudAppend01ProvesRuntimeDenialMutationPossibilityAndRestoredDenial()
    {
        await using AuditRuntimeBoundary boundary = await AuditRuntimeBoundary.CreateAsync(Database.ConnectionString);
        await boundary.MigrateAsync();
        Guid actor = Guid.NewGuid();

        await AppendAsync(boundary, SuccessRequest(actor, "baseline-update", "aud-append-baseline-update"));
        await AppendAsync(boundary, SuccessRequest(actor, "baseline-delete", "aud-append-baseline-delete"));

        Assert.Equal(boundary.RuntimeRoleName, await boundary.ReadRuntimeCurrentUserAsync());
        await AssertMutationRejectedAsync(
            boundary,
            $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'forbidden' WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-baseline-update';");
        await AssertMutationRejectedAsync(
            boundary,
            $"DELETE FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-baseline-delete';");
        Assert.Equal(2L, await boundary.CountAllAuditsAsync());
        output.WriteLine("BASELINE_GREEN: actual runtime principal append succeeded; UPDATE and DELETE were rejected by SQLSTATE 55000.");

        await AssertDb01RuntimeBoundaryAsync(boundary);

        await boundary.ExecuteMigratorNonQueryAsync(
            $"ALTER TABLE {AuditPersistence.TableName} DISABLE TRIGGER {AuditPersistence.AppendOnlyTrigger};");

        try
        {
            int updated = await boundary.ExecuteRuntimeNonQueryAsync(
                $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'mutation-possible' WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-baseline-update';");
            int deleted = await boundary.ExecuteRuntimeNonQueryAsync(
                $"DELETE FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-baseline-delete';");

            Assert.Equal(1, updated);
            Assert.Equal(1, deleted);
            Assert.Equal(
                "mutation-possible",
                Assert.IsType<string>(await boundary.ExecuteRuntimeScalarAsync(
                    $"SELECT {AuditPersistence.TargetIdentifierColumn} FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-baseline-update';")));
            output.WriteLine("MUTATION_RED: non-superuser Migrator disabled only the intended trigger; the same runtime principal then completed UPDATE and DELETE.");
        }
        finally
        {
            await boundary.ExecuteMigratorNonQueryAsync(
                $"ALTER TABLE {AuditPersistence.TableName} ENABLE TRIGGER {AuditPersistence.AppendOnlyTrigger};");
        }

        await AppendAsync(boundary, SuccessRequest(actor, "restore-append", "aud-append-restore"));
        await AssertMutationRejectedAsync(
            boundary,
            $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'forbidden-again' WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-restore';");
        await AssertMutationRejectedAsync(
            boundary,
            $"DELETE FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = 'aud-append-restore';");
        Assert.Equal(2L, await boundary.CountAllAuditsAsync());
        output.WriteLine("RESTORE_GREEN: trigger restored; runtime UPDATE and DELETE are rejected again and normal writer append succeeds.");
    }

    private static async Task AssertDb01RuntimeBoundaryAsync(AuditRuntimeBoundary boundary)
    {
        bool[] attributes = await boundary.ReadRuntimeRoleAttributesAsync();
        Assert.All(attributes, Assert.False);

        Assert.Equal(3L, Convert.ToInt64(
            await boundary.ExecuteRuntimeScalarAsync(
                $"SELECT count(*) FROM public.\"{BankPersistence.MigrationsHistoryTableName}\";"),
            CultureInfo.InvariantCulture));

        PostgresException ddl = await Assert.ThrowsAsync<PostgresException>(
            () => boundary.ExecuteRuntimeNonQueryAsync("CREATE TABLE audit_runtime_must_not_create (id integer);"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ddl.SqlState);

        PostgresException historyMutation = await Assert.ThrowsAsync<PostgresException>(
            () => boundary.ExecuteRuntimeNonQueryAsync(
                $"UPDATE public.\"{BankPersistence.MigrationsHistoryTableName}\" SET \"ProductVersion\" = 'forbidden';"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, historyMutation.SqlState);
    }

    private static async Task AssertMutationRejectedAsync(
        AuditRuntimeBoundary boundary,
        string commandText)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => boundary.ExecuteRuntimeNonQueryAsync(commandText));

        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, exception.SqlState);
        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    private static async Task AppendAsync(
        AuditRuntimeBoundary boundary,
        AuditWriteRequest request)
    {
        await using BankDbContext context = boundary.CreateRuntimeContext();
        PostgreSqlAuditWriter writer = CreateWriter(context);
        bool committed = await writer.AppendInSeparateTransactionBeforeResultAsync(
            request,
            _ => Task.FromResult(true));
        Assert.True(committed);
    }

    private static PostgreSqlAuditWriter CreateWriter(BankDbContext context) =>
        new(
            context,
            new AuditOperationRegistry([RegisteredOperation]),
            new ApplicationTime(new FrozenTimeProvider(FrozenLocalTime)));

    private static AuditWriteRequest SuccessRequest(
        Guid actor,
        string target,
        string correlationId) =>
        new(
            actor,
            OperatorRole.Administrator,
            RegisteredOperation,
            target,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            correlationId);

    private static AuditWriteRequest FailureRequest(
        Guid actor,
        string target,
        string correlationId,
        string errorCode) =>
        new(
            actor,
            OperatorRole.Administrator,
            RegisteredOperation,
            target,
            AuditResult.Failure,
            errorCode,
            correlationId);

    private static Task<int> InsertCallerStateAsync(
        BankDbContext context,
        string scenario,
        string value) =>
        context.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {CallerFixtureTable} (scenario, value) VALUES ({{0}}, {{1}});",
            scenario,
            value);
}

internal sealed class ThrowOnAuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowForAudit(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ThrowForAudit(eventData);
        return ValueTask.FromResult(result);
    }

    private static void ThrowForAudit(DbContextEventData eventData)
    {
        if (eventData.Context?.ChangeTracker.Entries<AuditRecord>()
            .Any(entry => entry.State == EntityState.Added) == true)
        {
            throw new AuditPersistenceInjectionException();
        }
    }
}

internal sealed class AuditPersistenceInjectionException()
    : InvalidOperationException("Deterministic test-only Product Audit persistence failure.");

internal sealed class AuditRuntimeBoundary : IAsyncDisposable
{
    private static readonly TimeSpan MigrationBudget = TimeSpan.FromSeconds(120);

    private readonly string administrativeConnectionString;
    private bool disposed;

    private AuditRuntimeBoundary(
        string administrativeConnectionString,
        string migratorRoleName,
        string runtimeRoleName,
        string migratorConnectionString,
        string runtimeConnectionString)
    {
        this.administrativeConnectionString = administrativeConnectionString;
        MigratorRoleName = migratorRoleName;
        RuntimeRoleName = runtimeRoleName;
        MigratorConnectionString = migratorConnectionString;
        RuntimeConnectionString = runtimeConnectionString;
    }

    public string MigratorRoleName { get; }

    public string RuntimeRoleName { get; }

    public string MigratorConnectionString { get; }

    public string RuntimeConnectionString { get; }

    public static async Task<AuditRuntimeBoundary> CreateAsync(string administrativeConnectionString)
    {
        string identity = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string migratorRole = $"mbs_aud_m_{identity}";
        string runtimeRole = $"mbs_aud_a_{identity}";
        string migratorPassword = $"M{identity}";
        string runtimePassword = $"A{identity}";
        NpgsqlConnectionStringBuilder admin = new(administrativeConnectionString);

        NpgsqlConnectionStringBuilder migrator = new(administrativeConnectionString)
        {
            Username = migratorRole,
            Password = migratorPassword,
            Pooling = false,
        };
        NpgsqlConnectionStringBuilder runtime = new(administrativeConnectionString)
        {
            Username = runtimeRole,
            Password = runtimePassword,
            Pooling = false,
        };

        AuditRuntimeBoundary boundary = new(
            administrativeConnectionString,
            migratorRole,
            runtimeRole,
            migrator.ConnectionString,
            runtime.ConnectionString);

        try
        {
            await boundary.ExecuteAdministrativeNonQueryAsync(
                $"""
                 CREATE ROLE {QuoteIdentifier(migratorRole)} LOGIN PASSWORD '{migratorPassword}'
                     NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                 CREATE ROLE {QuoteIdentifier(runtimeRole)} LOGIN PASSWORD '{runtimePassword}'
                     NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                 GRANT CONNECT ON DATABASE {QuoteIdentifier(admin.Database!)} TO {QuoteIdentifier(migratorRole)};
                 GRANT CONNECT ON DATABASE {QuoteIdentifier(admin.Database!)} TO {QuoteIdentifier(runtimeRole)};
                 GRANT USAGE, CREATE ON SCHEMA public TO {QuoteIdentifier(migratorRole)};
                 GRANT USAGE ON SCHEMA public TO {QuoteIdentifier(runtimeRole)};
                 ALTER DEFAULT PRIVILEGES FOR ROLE {QuoteIdentifier(migratorRole)} IN SCHEMA public
                     GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {QuoteIdentifier(runtimeRole)};
                 """);
        }
        catch
        {
            await boundary.DisposeAsync();
            throw;
        }

        return boundary;
    }

    public async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(
            MigratorConnectionString,
            MigrationBudget,
            RuntimeRoleName);

        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected Audit runtime-boundary migration success. Output:\n{run.Output}");
    }

    public BankDbContext CreateRuntimeContext(params IInterceptor[] interceptors)
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(RuntimeConnectionString, BankPersistence.MigrationTimeoutSeconds);

        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new BankDbContext(options.Options);
    }

    public Task CreateCallerFixtureAsync() =>
        ExecuteMigratorNonQueryAsync(
            $"""
             CREATE TABLE {AuditPersistenceTests.CallerFixtureTable} (
                 scenario text PRIMARY KEY,
                 value text NOT NULL
             );
             """);

    public async Task<string> ReadRuntimeCurrentUserAsync() =>
        Assert.IsType<string>(await ExecuteRuntimeScalarAsync("SELECT current_user;"));

    public async Task<bool[]> ReadRuntimeRoleAttributesAsync()
    {
        await using NpgsqlConnection connection = await OpenAsync(RuntimeConnectionString);
        await using NpgsqlCommand command = new(
            "SELECT rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls FROM pg_roles WHERE rolname = current_user;",
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return [reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4)];
    }

    public async Task<long> CountAuditAsync(string correlationId) =>
        Convert.ToInt64(
            await ExecuteRuntimeScalarAsync(
                $"SELECT count(*) FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = '{correlationId}';"),
            CultureInfo.InvariantCulture);

    public async Task<long> CountAllAuditsAsync() =>
        Convert.ToInt64(
            await ExecuteRuntimeScalarAsync($"SELECT count(*) FROM {AuditPersistence.TableName};"),
            CultureInfo.InvariantCulture);

    public async Task<long> CountCallerStateAsync(string scenario) =>
        Convert.ToInt64(
            await ExecuteRuntimeScalarAsync(
                $"SELECT count(*) FROM {AuditPersistenceTests.CallerFixtureTable} WHERE scenario = '{scenario}';"),
            CultureInfo.InvariantCulture);

    public Task<int> ExecuteRuntimeNonQueryAsync(string commandText) =>
        ExecuteNonQueryAsync(RuntimeConnectionString, commandText);

    public Task<object?> ExecuteRuntimeScalarAsync(string commandText) =>
        ExecuteScalarAsync(RuntimeConnectionString, commandText);

    public Task<int> ExecuteMigratorNonQueryAsync(string commandText) =>
        ExecuteNonQueryAsync(MigratorConnectionString, commandText);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await ExecuteAdministrativeNonQueryAsync(
            $"""
             DROP OWNED BY {QuoteIdentifier(RuntimeRoleName)};
             DROP OWNED BY {QuoteIdentifier(MigratorRoleName)};
             DROP ROLE IF EXISTS {QuoteIdentifier(RuntimeRoleName)};
             DROP ROLE IF EXISTS {QuoteIdentifier(MigratorRoleName)};
             """);

        disposed = true;
    }

    private Task<int> ExecuteAdministrativeNonQueryAsync(string commandText) =>
        ExecuteNonQueryAsync(administrativeConnectionString, commandText);

    private static async Task<int> ExecuteNonQueryAsync(string connectionString, string commandText)
    {
        await using NpgsqlConnection connection = await OpenAsync(connectionString);
        await using NpgsqlCommand command = new(commandText, connection);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ExecuteScalarAsync(string connectionString, string commandText)
    {
        await using NpgsqlConnection connection = await OpenAsync(connectionString);
        await using NpgsqlCommand command = new(commandText, connection);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
