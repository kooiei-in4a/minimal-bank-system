extern alias api;

using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorMutation;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Issue #171 (WP2-OPR-MUT-01) Critical Mutations. Each proves BASELINE_GREEN -&gt;
/// MUTATION_RED -&gt; RESTORE_GREEN against the real ASP.NET Core pipeline and a real PostgreSQL
/// database, with an explicit semantic failure signature for the targeted invariant/control -
/// never merely a generic failing HTTP status.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset FrozenUtcNow = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string SeedPlaintextPassword = "oprmut-critical-seed-password-not-for-production";

    /// <summary>
    /// OPR-MUT-ADMIN-01: active-administrator invariant bypass. Semantic oracle: the committed
    /// active-administrator count reaches zero. The mutation swaps the production
    /// <see cref="IOperatorMutationLockStrategy"/> for a double that locks only the target row
    /// (ignoring the active-administrator set) - the exact insufficient pattern the approved
    /// contract explicitly calls out as inadequate - so two concurrent, individually-plausible
    /// disables of the only two active administrators can both "win" the invariant check against
    /// stale pre-lock data.
    /// </summary>
    [Fact]
    public async Task OprMutAdmin01ActiveAdministratorInvariantBypassIsKilled()
    {
        await MigrateAsync();

        // BASELINE_GREEN: production lock strategy. Exactly two active administrators exist;
        // each concurrently disables the other. The invariant must permit only one to succeed.
        (Operator baselineAdmin1, Operator baselineAdmin2) = await SeedTwoActiveAdministratorsAsync(
            "oprmutadmin01.baseline.a", "oprmutadmin01.baseline.b");
        await ForceDisableAllActiveAdministratorsExceptAsync(baselineAdmin1.Id, baselineAdmin2.Id);
        await using (OperatorMutationAdmin01ApiFactory baseline = CreateFactory(services => { }))
        {
            using HttpClient client = baseline.CreateClient();
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(client, CreateToken(baselineAdmin1), "oprmutadmin01-baseline-a", baselineAdmin2.Id),
                SendDisableAsync(client, CreateToken(baselineAdmin2), "oprmutadmin01-baseline-b", baselineAdmin1.Id));

            int successCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }

            Assert.Equal(1, successCount);
        }

        Assert.True(
            await CountActiveAmongAsync(baselineAdmin1.Id, baselineAdmin2.Id) >= 1,
            "OPR-MUT-ADMIN-01 BASELINE_GREEN: the production lock strategy unexpectedly allowed the active-administrator count to reach zero.");

        // MUTATION_RED: the lock strategy is replaced with a double that locks only the target
        // row, modeling the read-count-then-write pattern the approved contract calls insufficient.
        (Operator mutatedAdmin1, Operator mutatedAdmin2) = await SeedTwoActiveAdministratorsAsync(
            "oprmutadmin01.mutated.a", "oprmutadmin01.mutated.b");
        await ForceDisableAllActiveAdministratorsExceptAsync(mutatedAdmin1.Id, mutatedAdmin2.Id);
        await using (OperatorMutationAdmin01ApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationLockStrategy>();
            services.AddScoped<IOperatorMutationLockStrategy, TargetOnlyLockStrategy>();
        }))
        {
            using HttpClient client = mutated.CreateClient();
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(client, CreateToken(mutatedAdmin1), "oprmutadmin01-mutated-a", mutatedAdmin2.Id),
                SendDisableAsync(client, CreateToken(mutatedAdmin2), "oprmutadmin01-mutated-b", mutatedAdmin1.Id));

            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        long mutatedActiveAdministratorCount = await CountActiveAmongAsync(mutatedAdmin1.Id, mutatedAdmin2.Id);
        Assert.True(
            mutatedActiveAdministratorCount == 0,
            $"OPR-MUT-ADMIN-01 MUTATION_RED: expected the target-only lock strategy to allow the committed active-administrator count (among the two seeded administrators) to reach zero, but observed {mutatedActiveAdministratorCount}.");

        // RESTORE_GREEN: back to the production lock strategy. The invariant holds again.
        (Operator restoredAdmin1, Operator restoredAdmin2) = await SeedTwoActiveAdministratorsAsync(
            "oprmutadmin01.restored.a", "oprmutadmin01.restored.b");
        await ForceDisableAllActiveAdministratorsExceptAsync(restoredAdmin1.Id, restoredAdmin2.Id);
        await using (OperatorMutationAdmin01ApiFactory restored = CreateFactory(services => { }))
        {
            using HttpClient client = restored.CreateClient();
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(client, CreateToken(restoredAdmin1), "oprmutadmin01-restored-a", restoredAdmin2.Id),
                SendDisableAsync(client, CreateToken(restoredAdmin2), "oprmutadmin01-restored-b", restoredAdmin1.Id));

            int successCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }

            Assert.Equal(1, successCount);
        }

        Assert.True(
            await CountActiveAmongAsync(restoredAdmin1.Id, restoredAdmin2.Id) >= 1,
            "OPR-MUT-ADMIN-01 RESTORE_GREEN: production code must not allow the active-administrator count to reach zero.");
    }

    /// <summary>
    /// OPR-MUT-AUTH-01: authorization-state invalidation bypass. Semantic oracle: an old
    /// authenticated JWT remains authorized after a successful security-relevant mutation. The
    /// mutation swaps the production <see cref="IOperatorMutationSuccessCommitter"/> for a double
    /// that applies the state/role change but reverts only the authorization-state version and
    /// security stamp fields before persisting - a control that changed state/role but forgot to
    /// invalidate prior authenticated sessions.
    /// </summary>
    [Fact]
    public async Task OprMutAuth01AuthorizationStateInvalidationBypassIsKilled()
    {
        await MigrateAsync();

        // BASELINE_GREEN: production commit path. A pre-mutation token must be rejected on the
        // next request after a successful mutation.
        Operator baselineAdmin = await SeedOperatorAsync("oprmutauth01.baseline.admin", OperatorRole.Administrator);
        Operator baselineTarget = await SeedOperatorAsync("oprmutauth01.baseline.target", OperatorRole.Teller);
        string baselineStaleToken = CreateToken(baselineTarget);
        await using (OperatorMutationAdmin01ApiFactory baseline = CreateFactory(services => { }))
        {
            using HttpClient client = baseline.CreateClient();
            using HttpResponseMessage mutationResponse = await SendRoleChangeAsync(
                client, CreateToken(baselineAdmin), "oprmutauth01-baseline-mutation", baselineTarget.Id, "administrator");
            Assert.Equal(HttpStatusCode.OK, mutationResponse.StatusCode);

            using HttpResponseMessage staleResponse = await SendAuthenticatedProbeAsync(
                client, baselineStaleToken, "oprmutauth01-baseline-stale");
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                staleResponse.StatusCode);
        }

        // MUTATION_RED: the commit path bypasses authorization-state invalidation.
        Operator mutatedAdmin = await SeedOperatorAsync("oprmutauth01.mutated.admin", OperatorRole.Administrator);
        Operator mutatedTarget = await SeedOperatorAsync("oprmutauth01.mutated.target", OperatorRole.Teller);
        string mutatedStaleToken = CreateToken(mutatedTarget);
        HttpStatusCode staleResponseStatusAfterBypass;
        await using (OperatorMutationAdmin01ApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationSuccessCommitter>();
            services.AddScoped<IOperatorMutationSuccessCommitter, AuthorizationInvalidationBypassSuccessCommitter>();
        }))
        {
            using HttpClient client = mutated.CreateClient();
            using HttpResponseMessage mutationResponse = await SendRoleChangeAsync(
                client, CreateToken(mutatedAdmin), "oprmutauth01-mutated-mutation", mutatedTarget.Id, "administrator");
            Assert.Equal(HttpStatusCode.OK, mutationResponse.StatusCode);

            using HttpResponseMessage staleResponse = await SendAuthenticatedProbeAsync(
                client, mutatedStaleToken, "oprmutauth01-mutated-stale");
            staleResponseStatusAfterBypass = staleResponse.StatusCode;
        }

        Assert.True(
            staleResponseStatusAfterBypass == HttpStatusCode.OK,
            $"OPR-MUT-AUTH-01 MUTATION_RED: expected the authorization-state-invalidation-bypass commit path to leave the pre-mutation token authorized (200), but observed {(int)staleResponseStatusAfterBypass}.");

        // RESTORE_GREEN: back to the production commit path. Invalidation holds again.
        Operator restoredAdmin = await SeedOperatorAsync("oprmutauth01.restored.admin", OperatorRole.Administrator);
        Operator restoredTarget = await SeedOperatorAsync("oprmutauth01.restored.target", OperatorRole.Teller);
        string restoredStaleToken = CreateToken(restoredTarget);
        await using (OperatorMutationAdmin01ApiFactory restored = CreateFactory(services => { }))
        {
            using HttpClient client = restored.CreateClient();
            using HttpResponseMessage mutationResponse = await SendRoleChangeAsync(
                client, CreateToken(restoredAdmin), "oprmutauth01-restored-mutation", restoredTarget.Id, "administrator");
            Assert.Equal(HttpStatusCode.OK, mutationResponse.StatusCode);

            using HttpResponseMessage staleResponse = await SendAuthenticatedProbeAsync(
                client, restoredStaleToken, "oprmutauth01-restored-stale");
            Assert.Equal(HttpStatusCode.Unauthorized, staleResponse.StatusCode);
        }
    }

    /// <summary>
    /// OPR-MUT-AUD-01: required mutation Audit / atomicity bypass. Semantic oracle: the target
    /// state or role commits without the required success Audit. The mutation swaps the
    /// production <see cref="IOperatorMutationSuccessCommitter"/> for a double that commits the
    /// state/role change and the caller transaction BEFORE attempting the required success Audit,
    /// independent of that commit.
    /// </summary>
    [Fact]
    public async Task OprMutAud01RequiredSuccessAuditAtomicityBypassIsKilled()
    {
        await MigrateAsync();

        // BASELINE_GREEN: production commit path with a required-Audit failure injected. The
        // state/role change and the required success Audit share one transaction, so the failed
        // Audit rolls the state/role change back too.
        Operator baselineAdmin = await SeedOperatorAsync("oprmutaud01.baseline.admin", OperatorRole.Administrator);
        Operator baselineTarget = await SeedOperatorAsync("oprmutaud01.baseline.target", OperatorRole.Teller);
        using (OprMutAuditFailureProbe baselineProbe = new())
        {
            await using OperatorMutationAdmin01ApiFactory baseline = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(baselineProbe);
                services.AddScoped<IAuditWriter, OprMutRequiredSuccessAuditFailureWriter>();
            });
            using HttpClient client = baseline.CreateClient();
            using HttpResponseMessage response = await SendDisableAsync(
                client, CreateToken(baselineAdmin), "oprmutaud01-baseline", baselineTarget.Id);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, baselineProbe.InvocationCount);
        }

        Operator baselinePersisted = await ReadOperatorAsync(baselineTarget.Id);
        Assert.True(
            baselinePersisted.State == OperatorState.Active,
            "OPR-MUT-AUD-01 BASELINE_GREEN: production code unexpectedly left a committed state change after a required success Audit failure.");
        Assert.Equal(0L, await CountAuditsAsync("oprmutaud01-baseline"));

        // MUTATION_RED: the commit path commits the state change independently of the required
        // Audit, which is only attempted (and fails) afterward.
        Operator mutatedAdmin = await SeedOperatorAsync("oprmutaud01.mutated.admin", OperatorRole.Administrator);
        Operator mutatedTarget = await SeedOperatorAsync("oprmutaud01.mutated.target", OperatorRole.Teller);
        using (OprMutAuditFailureProbe mutatedProbe = new())
        {
            await using OperatorMutationAdmin01ApiFactory mutated = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(mutatedProbe);
                services.AddScoped<IAuditWriter, OprMutRequiredSuccessAuditFailureWriter>();
                services.RemoveAll<IOperatorMutationSuccessCommitter>();
                services.AddScoped<IOperatorMutationSuccessCommitter, NonAtomicOperatorMutationSuccessCommitter>();
            });
            using HttpClient client = mutated.CreateClient();
            using HttpResponseMessage response = await SendDisableAsync(
                client, CreateToken(mutatedAdmin), "oprmutaud01-mutated", mutatedTarget.Id);

            // The client-observable outcome still looks like a safe failure...
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        // ...but the semantic invariant is broken: the state change already committed before the
        // required Audit write was even attempted.
        Operator mutatedPersisted = await ReadOperatorAsync(mutatedTarget.Id);
        long mutatedAuditCount = await CountAuditsAsync("oprmutaud01-mutated");
        Assert.True(
            mutatedPersisted.State == OperatorState.Disabled,
            "OPR-MUT-AUD-01 MUTATION_RED: expected the non-atomic commit path to leave a committed state change despite the required success Audit failing to persist.");
        Assert.Equal(0L, mutatedAuditCount);

        // RESTORE_GREEN: back to the production commit path with the same Audit failure. The
        // atomicity invariant holds again.
        Operator restoredAdmin = await SeedOperatorAsync("oprmutaud01.restored.admin", OperatorRole.Administrator);
        Operator restoredTarget = await SeedOperatorAsync("oprmutaud01.restored.target", OperatorRole.Teller);
        using (OprMutAuditFailureProbe restoredProbe = new())
        {
            await using OperatorMutationAdmin01ApiFactory restored = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(restoredProbe);
                services.AddScoped<IAuditWriter, OprMutRequiredSuccessAuditFailureWriter>();
            });
            using HttpClient client = restored.CreateClient();
            using HttpResponseMessage response = await SendDisableAsync(
                client, CreateToken(restoredAdmin), "oprmutaud01-restored", restoredTarget.Id);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(1, restoredProbe.InvocationCount);
        }

        Operator restoredPersisted = await ReadOperatorAsync(restoredTarget.Id);
        Assert.True(
            restoredPersisted.State == OperatorState.Active,
            "OPR-MUT-AUD-01 RESTORE_GREEN: production code must not leave a committed state change after a required success Audit failure.");
        Assert.Equal(0L, await CountAuditsAsync("oprmutaud01-restored"));
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private OperatorMutationAdmin01ApiFactory CreateFactory(Action<IServiceCollection> configureServices) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected OPR-MUT critical mutation migration success. Output:{Environment.NewLine}{run.Output}");
    }

    /// <summary>
    /// Forces every active administrator other than the given identifiers to disabled via a
    /// direct write, bypassing the API. Every phase of this test seeds a fresh pair of
    /// administrators in the same shared per-test-method database; without this, an earlier
    /// phase's surviving administrator would give a later phase's pair extra headroom and the
    /// active-administrator invariant would no longer bind on that pair alone.
    /// </summary>
    private async Task ForceDisableAllActiveAdministratorsExceptAsync(params Guid[] keep)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = '{OperatorPersistence.DisabledStateToken}'
             WHERE {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}'
               AND {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
               AND NOT ({OperatorPersistence.IdColumn} = ANY(@keep));
             """,
            connection);
        command.Parameters.AddWithValue("keep", keep);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(Operator First, Operator Second)> SeedTwoActiveAdministratorsAsync(
        string firstUserName,
        string secondUserName)
    {
        Operator first = await SeedOperatorAsync(firstUserName, OperatorRole.Administrator);
        Operator second = await SeedOperatorAsync(secondUserName, OperatorRole.Administrator);
        return (first, second);
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using BankDbContext context = CreateContext();
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.AsNoTracking().SingleAsync(candidate => candidate.Id == identifier);
    }

    /// <summary>
    /// Counts active administrators restricted to a specific set of seeded identifiers, not the
    /// whole table - other test iterations in this same suite (baseline/mutated/restored) seed
    /// their own administrators in the same shared database, so a global count would be polluted
    /// by earlier iterations' surviving administrators.
    /// </summary>
    private async Task<long> CountActiveAmongAsync(params Guid[] identifiers)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT count(*) FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}'
               AND {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
               AND {OperatorPersistence.IdColumn} = ANY(@ids);
             """,
            connection);
        command.Parameters.AddWithValue("ids", identifiers);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountAuditsAsync(string correlationId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT count(*) FROM audit_records WHERE correlation_id = @correlation_id;",
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage> SendDisableAsync(
        HttpClient client,
        string token,
        string correlationId,
        Guid operatorIdentifier)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{operatorIdentifier:D}/disable");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRoleChangeAsync(
        HttpClient client,
        string token,
        string correlationId,
        Guid operatorIdentifier,
        string role)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{operatorIdentifier:D}/role");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = System.Net.Http.Json.JsonContent.Create(new { role });
        return await client.SendAsync(request);
    }

    /// <summary>
    /// A minimal authenticated probe: any authorized endpoint works to observe whether the
    /// presented token is still accepted. The Operator list endpoint is administrator-only and has
    /// no side effects.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAuthenticatedProbeAsync(
        HttpClient client,
        string token,
        string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: "minimal-bank-system",
            audience: "minimal-bank-system-api",
            claims:
            [
                new System.Security.Claims.Claim(
                    JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new System.Security.Claims.Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class OperatorMutationAdmin01ApiFactory(
    string connectionString,
    Action<IServiceCollection> configureServices) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(JwtAuthnOptions.SigningKeyConfigurationKey, TestJwtConfiguration.SigningKey);
        builder.UseSetting(
            $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
            connectionString);
        builder.ConfigureServices(configureServices);
    }
}

/// <summary>
/// OPR-MUT-ADMIN-01 MUTATION_RED double: locks only the target row and derives the
/// active-administrator count from a plain unlocked snapshot read - the exact
/// "target Operatorだけをlockしてread-count-then-writeする実装では不十分です" pattern the approved
/// contract calls out by name as insufficient. Because the count is not backed by any row lock,
/// two concurrent requests each targeting a different one of the only two active administrators
/// can both observe count=2 before either commits and both proceed. Test-composition-only; never
/// referenced by production Program.cs.
/// </summary>
internal sealed class TargetOnlyLockStrategy : IOperatorMutationLockStrategy
{
    public async Task<IReadOnlyDictionary<Guid, Operator>> LockAsync(
        BankDbContext persistence,
        Guid targetIdentifier,
        bool lockActiveAdministratorSet,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, Operator> locked = [];

        Operator? target = await persistence.Operators
            .FromSqlInterpolated($"SELECT * FROM operators WHERE id = {targetIdentifier} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (target is not null)
        {
            locked[targetIdentifier] = target;
        }

        if (lockActiveAdministratorSet)
        {
            // Deliberately unlocked: does not block on and is not serialized against a concurrent
            // transaction running this same insufficient strategy against a different target.
            List<Operator> unlockedActiveAdministrators = await persistence.Operators
                .AsNoTracking()
                .Where(candidate => candidate.Role == OperatorRole.Administrator && candidate.State == OperatorState.Active)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (Operator administrator in unlockedActiveAdministrators)
            {
                locked.TryAdd(administrator.Id, administrator);
            }
        }

        return locked;
    }
}

/// <summary>
/// OPR-MUT-AUTH-01 MUTATION_RED double: applies the state/role change but reverts the
/// authorization-state version and security stamp fields the domain method just bumped before
/// persisting, modeling a control that changed state/role but forgot to invalidate prior
/// authenticated sessions. Test-composition-only; never referenced by production Program.cs.
/// </summary>
internal sealed class AuthorizationInvalidationBypassSuccessCommitter(IAuditWriter auditWriter)
    : IOperatorMutationSuccessCommitter
{
    private static readonly PropertyInfo AuthorizationStateVersionProperty =
        typeof(Operator).GetProperty(nameof(Operator.AuthorizationStateVersion))!;

    private static readonly PropertyInfo SecurityStampProperty =
        typeof(Operator).GetProperty(nameof(Operator.SecurityStamp))!;

    public async Task CommitAsync(
        BankDbContext persistence,
        IDbContextTransaction transaction,
        Operator target,
        Action<Operator> applyMutation,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken)
    {
        int originalVersion = target.AuthorizationStateVersion;
        string originalStamp = target.SecurityStamp;

        applyMutation(target);

        AuthorizationStateVersionProperty.SetValue(target, originalVersion);
        SecurityStampProperty.SetValue(target, originalStamp);

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// OPR-MUT-AUD-01 MUTATION_RED double: the state/role change and the caller transaction commit
/// independently of the required success Audit, which is only attempted afterward. If that Audit
/// write fails, the state/role change remains committed - the exact atomicity/control loss the
/// mutation must model. Test-composition-only; never referenced by production Program.cs.
/// </summary>
internal sealed class NonAtomicOperatorMutationSuccessCommitter(IAuditWriter auditWriter)
    : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        BankDbContext persistence,
        IDbContextTransaction transaction,
        Operator target,
        Action<Operator> applyMutation,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken)
    {
        applyMutation(target);
        // No caller-owned transaction survives past this point: the state/role change commits
        // immediately and independently of the Audit write below.
        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                successAudit,
                _ => Task.FromResult(true),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class OprMutAuditFailureProbe : IDisposable
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);

    public void Dispose()
    {
    }
}

/// <summary>
/// Fails the required success Audit primitive, matching how both the production and mutated
/// commit paths attempt to persist the success Audit.
/// </summary>
internal sealed class OprMutRequiredSuccessAuditFailureWriter(OprMutAuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorMutationAud01FailureInjectionException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorMutationAud01FailureInjectionException();
    }
}

internal sealed class OperatorMutationAud01FailureInjectionException()
    : InvalidOperationException("Deterministic test-only OPR-MUT-AUD-01 required success Audit failure.");
