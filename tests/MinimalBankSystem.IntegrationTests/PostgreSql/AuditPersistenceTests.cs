using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using Npgsql;
using Xunit.Abstractions;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// AUD-01 semantic evidence against PostgreSQL and the designated API runtime principal. The raw
/// caller-state relation and concrete operation identifiers are disposable verification fixtures;
/// neither is part of BankDbContext, a product migration or production composition.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuditPersistenceTests(
    PostgreSqlContainerFixture fixture,
    ITestOutputHelper output)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string LogicalFieldsOperation = "verification.audit.logical-fields";
    private const string CallerTransactionOperation = "verification.audit.caller-transaction";
    private const string SeparateTransactionOperation = "verification.audit.separate-transaction";
    private const string AppendOnlyOperation = "verification.audit.append-only";
    private const string UnregisteredOperation = "verification.audit.unregistered";
    private const string SensitiveSentinel = "AUDIT_SECRET_SENTINEL_NOT_A_REAL_CREDENTIAL";
    private const string CallerFixtureTable = "audit_verification_caller_state";

    private static readonly DateTimeOffset FrozenUtc =
        new(2026, 8, 15, 0, 30, 45, TimeSpan.Zero);

    private static readonly Guid ActorIdentifier = Guid.CreateVersion7(FrozenUtc);

    [Fact]
    public async Task LogicalFieldsUseUuidV7UtcSnapshotsAndExcludeSensitiveOperatorMaterial()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);

        await environment.ExecuteRuntimeNonQueryAsync(
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
                 {OperatorPersistence.UpdatedAtColumn})
             VALUES (
                 @id, 'audit.snapshot.viewer', 'AUDIT.SNAPSHOT.VIEWER', @password_hash,
                 'audit-test-security-stamp', 'active', 'viewer', 1, @created_at, @updated_at);
             """,
            new NpgsqlParameter("id", ActorIdentifier),
            new NpgsqlParameter("password_hash", SensitiveSentinel),
            new NpgsqlParameter("created_at", FrozenUtc),
            new NpgsqlParameter("updated_at", FrozenUtc));

        await using (BankDbContext context = environment.CreateRuntimeContext())
        {
            AuditWriter writer = CreateWriter(context, LogicalFieldsOperation);
            await using var transaction = await context.Database.BeginTransactionAsync();

            await writer.AppendInCallerTransactionAsync(new AuditWriteRequest(
                ActorIdentifier,
                OperatorRole.Viewer,
                LogicalFieldsOperation,
                ActorIdentifier.ToString("D", CultureInfo.InvariantCulture),
                AuditResult.Failure,
                "operator_not_found",
                "correlation-audit-logical-fields"));

            await transaction.CommitAsync();
        }

        // This models a later approved current-role/state change. Product Audit must retain the
        // operation-time snapshots and must not depend on a live Operator relationship.
        await environment.ExecuteRuntimeNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.FixedRoleColumn} = 'teller',
                 {OperatorPersistence.StateColumn} = 'disabled',
                 {OperatorPersistence.UpdatedAtColumn} = @updated_at
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            new NpgsqlParameter("id", ActorIdentifier),
            new NpgsqlParameter("updated_at", FrozenUtc.AddMinutes(5)));

        await using BankDbContext readContext = environment.CreateRuntimeContext();
        AuditRecord stored = await readContext.AuditRecords.AsNoTracking().SingleAsync();

        Assert.Equal(7, stored.AuditId.Version);
        Assert.Equal(ActorIdentifier, stored.ActorIdentifier);
        Assert.Equal(OperatorRole.Viewer, stored.ActorRole);
        Assert.Equal(LogicalFieldsOperation, stored.OperationIdentifier);
        Assert.Equal(ActorIdentifier.ToString("D", CultureInfo.InvariantCulture), stored.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, stored.Result);
        Assert.Equal("operator_not_found", stored.FailureBusinessErrorCode);
        Assert.Equal("correlation-audit-logical-fields", stored.CorrelationId);
        Assert.Equal(FrozenUtc, stored.AuditTime);
        Assert.Equal(TimeSpan.Zero, stored.AuditTime.Offset);

        Assert.Equal(
            "timestamp with time zone",
            await environment.ExecuteMigratorScalarAsync<string>(
                $"""
                 SELECT data_type
                 FROM information_schema.columns
                 WHERE table_schema = 'public'
                   AND table_name = '{AuditPersistence.TableName}'
                   AND column_name = '{AuditPersistence.AuditTimeColumn}';
                 """));
        Assert.Equal(
            0L,
            await environment.ExecuteMigratorScalarAsync<long>(
                $"""
                 SELECT count(*)
                 FROM information_schema.table_constraints
                 WHERE table_schema = 'public'
                   AND table_name = '{AuditPersistence.TableName}'
                   AND constraint_type = 'FOREIGN KEY';
                 """));

        string auditJson = await environment.ExecuteMigratorScalarAsync<string>(
            $"SELECT to_jsonb(record)::text FROM {AuditPersistence.TableName} AS record;");
        Assert.Contains(LogicalFieldsOperation, auditJson, StringComparison.Ordinal);
        Assert.Contains("correlation-audit-logical-fields", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(SensitiveSentinel, auditJson, StringComparison.Ordinal);
        Assert.Equal(
            SensitiveSentinel,
            await environment.ExecuteRuntimeScalarAsync<string>(
                $"SELECT {OperatorPersistence.PasswordHashColumn} FROM {OperatorPersistence.TableName};"));
    }

    [Fact]
    public async Task CallerTransactionCommitsAndRollsBackTheFixtureAndAuditTogether()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);
        await environment.CreateCallerFixtureAsync();

        await using (BankDbContext context = environment.CreateRuntimeContext())
        {
            AuditWriter writer = CreateWriter(context, CallerTransactionOperation);
            await using var transaction = await context.Database.BeginTransactionAsync();

            await context.Database.ExecuteSqlRawAsync(
                $"INSERT INTO {CallerFixtureTable} (id, value) VALUES (1, 'committed');");
            await writer.AppendInCallerTransactionAsync(
                CreateSuccessRequest(CallerTransactionOperation, "caller-state-1"));
            await transaction.CommitAsync();
        }

        Assert.Equal(1L, await environment.CountAsync(CallerFixtureTable));
        Assert.Equal(1L, await environment.CountAsync(AuditPersistence.TableName));

        await using (BankDbContext context = environment.CreateRuntimeContext())
        {
            AuditWriter writer = CreateWriter(context, CallerTransactionOperation);
            await using var transaction = await context.Database.BeginTransactionAsync();

            await context.Database.ExecuteSqlRawAsync(
                $"INSERT INTO {CallerFixtureTable} (id, value) VALUES (2, 'rolled-back');");
            await writer.AppendInCallerTransactionAsync(
                CreateSuccessRequest(CallerTransactionOperation, "caller-state-2"));
            await transaction.RollbackAsync();
        }

        Assert.Equal(1L, await environment.CountAsync(CallerFixtureTable));
        Assert.Equal(1L, await environment.CountAsync(AuditPersistence.TableName));
    }

    [Fact]
    public async Task RequiredAuditFailureRollsBackCallerStateAndExposesNoPartialSuccess()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);
        await environment.CreateCallerFixtureAsync();

        bool successExposed = false;
        await using BankDbContext context = environment.CreateRuntimeContext(
            new RejectAuditSaveChangesInterceptor());
        AuditWriter writer = CreateWriter(context, CallerTransactionOperation);
        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {CallerFixtureTable} (id, value) VALUES (1, 'must-not-commit');");

        await Assert.ThrowsAsync<AuditInjectedFailureException>(async () =>
        {
            await writer.AppendInCallerTransactionAsync(
                CreateSuccessRequest(CallerTransactionOperation, "caller-state-failure"));
            successExposed = true;
            await transaction.CommitAsync();
        });

        Assert.False(successExposed);
        Assert.Equal(0L, await environment.CountAsync(CallerFixtureTable));
        Assert.Equal(0L, await environment.CountAsync(AuditPersistence.TableName));
    }

    [Fact]
    public async Task SeparateTransactionCommitsBeforeReturningAndFailureReturnsNoSuccess()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);

        string? exposedResult = null;
        await using (BankDbContext context = environment.CreateRuntimeContext())
        {
            AuditWriter writer = CreateWriter(context, SeparateTransactionOperation);
            exposedResult = await writer.AppendInSeparateTransactionBeforeSuccessAsync(
                CreateSuccessRequest(SeparateTransactionOperation, "query-target-success"),
                "success-payload");
        }

        Assert.Equal("success-payload", exposedResult);
        Assert.Equal(1L, await environment.CountAsync(AuditPersistence.TableName));

        exposedResult = null;
        await using (BankDbContext context = environment.CreateRuntimeContext(
            new RejectAuditSaveChangesInterceptor()))
        {
            AuditWriter writer = CreateWriter(context, SeparateTransactionOperation);

            await Assert.ThrowsAsync<AuditInjectedFailureException>(async () =>
            {
                exposedResult = await writer.AppendInSeparateTransactionBeforeSuccessAsync(
                    CreateSuccessRequest(SeparateTransactionOperation, "query-target-failure"),
                    "must-not-be-exposed");
            });
        }

        Assert.Null(exposedResult);
        Assert.Equal(1L, await environment.CountAsync(AuditPersistence.TableName));
    }

    [Fact]
    public async Task UnregisteredOperationRollsBackCallerStateFailClosed()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);
        await environment.CreateCallerFixtureAsync();

        await using BankDbContext context = environment.CreateRuntimeContext();
        AuditWriter writer = CreateWriter(context);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {CallerFixtureTable} (id, value) VALUES (1, 'must-not-commit');");

        await Assert.ThrowsAsync<UnregisteredAuditOperationException>(() =>
            writer.AppendInCallerTransactionAsync(
                CreateSuccessRequest(UnregisteredOperation, "unregistered-target")));

        Assert.Equal(0L, await environment.CountAsync(CallerFixtureTable));
        Assert.Equal(0L, await environment.CountAsync(AuditPersistence.TableName));
    }

    [Fact]
    public async Task AudAppend01RuntimePrincipalMutationSequenceHasTheRequiredSemanticSignature()
    {
        await using AuditRuntimeEnvironment environment =
            await AuditRuntimeEnvironment.CreateAsync(Database.ConnectionString);

        Guid baselineAuditId = await AppendAndReturnIdAsync(environment, "append-only-baseline");
        Assert.Equal(
            AuditRuntimeEnvironment.ApiRole,
            await environment.ExecuteRuntimeScalarAsync<string>("SELECT current_user;"));
        await AssertRuntimeMutationDeniedAsync(environment, baselineAuditId, "UPDATE");
        await AssertRuntimeMutationDeniedAsync(environment, baselineAuditId, "DELETE");
        output.WriteLine("AUD-APPEND-01: BASELINE_GREEN");
        output.WriteLine("AUD-APPEND-01: ACTUAL_RUNTIME_PRINCIPAL=minimal_bank_api");
        output.WriteLine("AUD-APPEND-01: NORMAL_APPEND_SUCCEEDED_AND_UPDATE_DELETE_REJECTED");

        await environment.ExecuteMigratorNonQueryAsync(
            $"ALTER TABLE {AuditPersistence.TableName} DISABLE TRIGGER {AuditPersistence.AppendOnlyTrigger};");

        try
        {
            Assert.Equal(
                1,
                await environment.ExecuteRuntimeNonQueryAsync(
                    $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'mutated' WHERE {AuditPersistence.AuditIdColumn} = @id;",
                    new NpgsqlParameter("id", baselineAuditId)));
            Assert.Equal(
                1,
                await environment.ExecuteRuntimeNonQueryAsync(
                    $"DELETE FROM {AuditPersistence.TableName} WHERE {AuditPersistence.AuditIdColumn} = @id;",
                    new NpgsqlParameter("id", baselineAuditId)));
            output.WriteLine("AUD-APPEND-01: MUTATION_RED");
            output.WriteLine("AUD-APPEND-01: SEMANTIC_FAILURE=runtime-principal-update-and-delete-became-possible");
        }
        finally
        {
            await environment.ExecuteMigratorNonQueryAsync(
                $"ALTER TABLE {AuditPersistence.TableName} ENABLE TRIGGER {AuditPersistence.AppendOnlyTrigger};");
        }

        Guid restoredAuditId = await AppendAndReturnIdAsync(environment, "append-only-restored");
        await AssertRuntimeMutationDeniedAsync(environment, restoredAuditId, "UPDATE");
        await AssertRuntimeMutationDeniedAsync(environment, restoredAuditId, "DELETE");
        output.WriteLine("AUD-APPEND-01: RESTORE_GREEN");
        output.WriteLine("AUD-APPEND-01: RESTORED_APPEND_SUCCEEDED_AND_UPDATE_DELETE_REJECTED");
        output.WriteLine("AUD-APPEND-01: KILLED");
    }

    private static AuditWriter CreateWriter(
        BankDbContext context,
        params string[] registeredOperations) =>
        new AuditWriter(
            context,
            new AuditOperationRegistry(
                registeredOperations.Select(operation => new AuditOperationRegistration(operation))),
            new ApplicationTime(new AuditFrozenTimeProvider(FrozenUtc)));

    private static AuditWriteRequest CreateSuccessRequest(string operation, string target) =>
        new(
            ActorIdentifier,
            OperatorRole.Teller,
            operation,
            target,
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            $"correlation-{target}");

    private static async Task<Guid> AppendAndReturnIdAsync(
        AuditRuntimeEnvironment environment,
        string target)
    {
        await using BankDbContext context = environment.CreateRuntimeContext();
        AuditWriter writer = CreateWriter(context, AppendOnlyOperation);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await writer.AppendInCallerTransactionAsync(CreateSuccessRequest(AppendOnlyOperation, target));
        Guid auditId = context.AuditRecords.Local.Single().AuditId;
        await transaction.CommitAsync();
        return auditId;
    }

    private static async Task AssertRuntimeMutationDeniedAsync(
        AuditRuntimeEnvironment environment,
        Guid auditId,
        string mutation)
    {
        string sql = mutation switch
        {
            "UPDATE" =>
                $"UPDATE {AuditPersistence.TableName} SET {AuditPersistence.TargetIdentifierColumn} = 'prohibited' WHERE {AuditPersistence.AuditIdColumn} = @id;",
            "DELETE" =>
                $"DELETE FROM {AuditPersistence.TableName} WHERE {AuditPersistence.AuditIdColumn} = @id;",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() =>
            environment.ExecuteRuntimeNonQueryAsync(sql, new NpgsqlParameter("id", auditId)));
        Assert.Equal(AuditPersistence.AppendOnlySqlState, exception.SqlState);
        Assert.Contains(AuditPersistence.AppendOnlyErrorMarker, exception.MessageText, StringComparison.Ordinal);
    }

    private sealed class AuditRuntimeEnvironment : IAsyncDisposable
    {
        public const string MigratorRole = "minimal_bank_migrator";
        public const string ApiRole = "minimal_bank_api";

        private readonly string adminConnectionString;
        private readonly string migratorConnectionString;
        private readonly string runtimeConnectionString;
        private bool disposed;

        private AuditRuntimeEnvironment(
            string adminConnectionString,
            string migratorConnectionString,
            string runtimeConnectionString)
        {
            this.adminConnectionString = adminConnectionString;
            this.migratorConnectionString = migratorConnectionString;
            this.runtimeConnectionString = runtimeConnectionString;
        }

        public static async Task<AuditRuntimeEnvironment> CreateAsync(string adminConnectionString)
        {
            string migratorPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            string apiPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            NpgsqlConnectionStringBuilder admin = new(adminConnectionString);
            string database = admin.Database
                ?? throw new InvalidOperationException("The verification database name is required.");

            NpgsqlConnectionStringBuilder migrator = new(adminConnectionString)
            {
                Username = MigratorRole,
                Password = migratorPassword,
                Pooling = false,
            };
            NpgsqlConnectionStringBuilder runtime = new(adminConnectionString)
            {
                Username = ApiRole,
                Password = apiPassword,
                Pooling = false,
            };

            AuditRuntimeEnvironment environment = new(
                adminConnectionString,
                migrator.ConnectionString,
                runtime.ConnectionString);

            try
            {
                await environment.ExecuteAdminNonQueryAsync(
                    $"""
                     CREATE ROLE {MigratorRole} LOGIN PASSWORD '{migratorPassword}'
                         NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                     CREATE ROLE {ApiRole} LOGIN PASSWORD '{apiPassword}'
                         NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                     GRANT CONNECT ON DATABASE {QuoteIdentifier(database)} TO {MigratorRole};
                     GRANT CONNECT ON DATABASE {QuoteIdentifier(database)} TO {ApiRole};
                     GRANT USAGE, CREATE ON SCHEMA public TO {MigratorRole};
                     GRANT USAGE ON SCHEMA public TO {ApiRole};
                     ALTER DEFAULT PRIVILEGES FOR ROLE {MigratorRole} IN SCHEMA public
                         GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {ApiRole};
                     """);

                await using BankDbContext context = environment.CreateMigratorContext();
                await context.Database.MigrateAsync();
                await context.Database.ExecuteSqlRawAsync(
                    $"REVOKE INSERT, UPDATE, DELETE ON TABLE public.\"{BankPersistence.MigrationsHistoryTableName}\" FROM {ApiRole};");
                await context.Database.ExecuteSqlRawAsync(
                    $"GRANT SELECT ON TABLE public.\"{BankPersistence.MigrationsHistoryTableName}\" TO {ApiRole};");

                return environment;
            }
            catch
            {
                await environment.DisposeAsync();
                throw;
            }
        }

        public BankDbContext CreateRuntimeContext(IInterceptor? interceptor = null) =>
            CreateContext(runtimeConnectionString, interceptor);

        public async Task CreateCallerFixtureAsync()
        {
            await ExecuteMigratorNonQueryAsync(
                $"CREATE TABLE {CallerFixtureTable} (id integer PRIMARY KEY, value text NOT NULL);");
        }

        public Task<long> CountAsync(string tableName) =>
            ExecuteMigratorScalarAsync<long>($"SELECT count(*) FROM {tableName};");

        public Task<int> ExecuteRuntimeNonQueryAsync(
            string sql,
            params NpgsqlParameter[] parameters) =>
            ExecuteNonQueryAsync(runtimeConnectionString, sql, parameters);

        public Task<int> ExecuteMigratorNonQueryAsync(
            string sql,
            params NpgsqlParameter[] parameters) =>
            ExecuteNonQueryAsync(migratorConnectionString, sql, parameters);

        public Task<T> ExecuteRuntimeScalarAsync<T>(string sql) =>
            ExecuteScalarAsync<T>(runtimeConnectionString, sql);

        public Task<T> ExecuteMigratorScalarAsync<T>(string sql) =>
            ExecuteScalarAsync<T>(migratorConnectionString, sql);

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            await using NpgsqlConnection connection = new(adminConnectionString);
            await connection.OpenAsync();

            if (await RoleExistsAsync(connection, ApiRole))
            {
                await ExecuteNonQueryAsync(connection, $"DROP OWNED BY {ApiRole};");
            }

            if (await RoleExistsAsync(connection, MigratorRole))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    $"ALTER DEFAULT PRIVILEGES FOR ROLE {MigratorRole} IN SCHEMA public REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM {ApiRole};");
                await ExecuteNonQueryAsync(connection, $"REASSIGN OWNED BY {MigratorRole} TO postgres;");
                await ExecuteNonQueryAsync(connection, $"DROP OWNED BY {MigratorRole};");
            }

            if (await RoleExistsAsync(connection, ApiRole))
            {
                await ExecuteNonQueryAsync(connection, $"DROP ROLE {ApiRole};");
            }

            if (await RoleExistsAsync(connection, MigratorRole))
            {
                await ExecuteNonQueryAsync(connection, $"DROP ROLE {MigratorRole};");
            }

            disposed = true;
        }

        private BankDbContext CreateMigratorContext() => CreateContext(migratorConnectionString);

        private static BankDbContext CreateContext(
            string connectionString,
            IInterceptor? interceptor = null)
        {
            DbContextOptionsBuilder<BankDbContext> options = new();
            options.UseBankPostgreSql(connectionString, BankPersistence.MigrationTimeoutSeconds);

            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            return new BankDbContext(options.Options);
        }

        private Task<int> ExecuteAdminNonQueryAsync(string sql) =>
            ExecuteNonQueryAsync(adminConnectionString, sql, []);

        private static async Task<int> ExecuteNonQueryAsync(
            string connectionString,
            string sql,
            params NpgsqlParameter[] parameters)
        {
            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new(sql, connection);
            command.Parameters.AddRange(parameters);
            return await command.ExecuteNonQueryAsync();
        }

        private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
        {
            await using NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new(sql, connection);
            object value = await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("PostgreSQL scalar query returned null.");
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static async Task<bool> RoleExistsAsync(NpgsqlConnection connection, string role)
        {
            await using NpgsqlCommand command = new(
                "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role);",
                connection);
            command.Parameters.AddWithValue("role", role);
            return Assert.IsType<bool>(await command.ExecuteScalarAsync());
        }

        private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql)
        {
            await using NpgsqlCommand command = new(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string identifier) =>
            $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

/// <summary>
/// Deterministic AUD-01 failure injection. This type exists only in the integration-test assembly;
/// production has no configuration, environment variable, request input or selectable service that
/// can reach it.
/// </summary>
internal sealed class RejectAuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.ChangeTracker.Entries<AuditRecord>()
            .Any(entry => entry.State == EntityState.Added) == true)
        {
            throw new AuditInjectedFailureException();
        }

        return ValueTask.FromResult(result);
    }
}

internal sealed class AuditInjectedFailureException()
    : InvalidOperationException("Deterministic test-only Product Audit persistence failure.");

internal sealed class AuditFrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
