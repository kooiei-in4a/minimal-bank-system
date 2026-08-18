extern alias api;

using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorMutation;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Auditing;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authentication;
using MinimalBankSystem.IntegrationTests.OperatorMutation;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorMutationCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "operator-mutation-critical-seed-not-for-production";

    /// <summary>
    /// OPR-MUT-ADMIN-01: active-administrator invariant bypass. Semantic oracle: the committed
    /// active-administrator count reaches zero. Two concurrent, individually-plausible disables
    /// of the only two active administrators must not both be allowed to "win" the invariant
    /// check against stale, unlocked data. A self-disable scenario cannot exercise this race
    /// (only one Operator would ever be involved), so this proof always uses two distinct
    /// administrators disabling each other.
    /// </summary>
    [Fact]
    public async Task OprMutAdmin01ActiveAdministratorInvariantIsProvenWithSemanticOracle()
    {
        await MigrateAsync();

        // BASELINE_GREEN: production service. Exactly two active administrators exist; each
        // concurrently disables the other. The invariant must permit only one to succeed.
        await ForceDisableAllActiveAdministratorsExceptAsync();
        (Operator baselineA, Operator baselineB) = await SeedTwoActiveAdministratorsAsync(
            "opr-mut-admin01.baseline.a", "opr-mut-admin01.baseline.b");
        await using (OperatorMutationApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(baselineClient, baselineB.Id, CreateToken(baselineA), "opr-mut-admin01-baseline-a"),
                SendDisableAsync(baselineClient, baselineA.Id, CreateToken(baselineB), "opr-mut-admin01-baseline-b"));
            int successCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }

            Assert.Equal(1, successCount);
        }

        Assert.True(
            await CountActiveAmongAsync(baselineA.Id, baselineB.Id) >= 1,
            "OPR-MUT-ADMIN-01 BASELINE_GREEN: the production lock strategy unexpectedly allowed the active-administrator count to reach zero.");

        // MUTATION_RED: the production IOperatorMutationService is replaced with a test-only
        // double that locks only the requested target row and reads the active-administrator
        // count from an unlocked snapshot, instead of locking the full active-administrator set -
        // the exact insufficient pattern the approved contract calls out by name.
        await ForceDisableAllActiveAdministratorsExceptAsync();
        (Operator mutatedA, Operator mutatedB) = await SeedTwoActiveAdministratorsAsync(
            "opr-mut-admin01.mutated.a", "opr-mut-admin01.mutated.b");
        await using (OperatorMutationApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationService>();
            services.AddScoped<IOperatorMutationService, TargetOnlyUnlockedCountMutationService>();
        }))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(mutatedClient, mutatedB.Id, CreateToken(mutatedA), "opr-mut-admin01-mutated-a"),
                SendDisableAsync(mutatedClient, mutatedA.Id, CreateToken(mutatedB), "opr-mut-admin01-mutated-b"));
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        long mutatedActiveAdministratorCount = await CountActiveAmongAsync(mutatedA.Id, mutatedB.Id);
        Assert.True(
            mutatedActiveAdministratorCount == 0,
            $"OPR-MUT-ADMIN-01 MUTATION_RED: expected the target-only, unlocked-count service to allow the committed active-administrator count (among the two seeded administrators) to reach zero, but observed {mutatedActiveAdministratorCount}.");

        // RESTORE_GREEN: back to the production service. The invariant holds again.
        await ForceDisableAllActiveAdministratorsExceptAsync();
        (Operator restoredA, Operator restoredB) = await SeedTwoActiveAdministratorsAsync(
            "opr-mut-admin01.restored.a", "opr-mut-admin01.restored.b");
        await using (OperatorMutationApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            HttpResponseMessage[] responses = await Task.WhenAll(
                SendDisableAsync(restoredClient, restoredB.Id, CreateToken(restoredA), "opr-mut-admin01-restored-a"),
                SendDisableAsync(restoredClient, restoredA.Id, CreateToken(restoredB), "opr-mut-admin01-restored-b"));
            int successCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }

            Assert.Equal(1, successCount);
        }

        Assert.True(
            await CountActiveAmongAsync(restoredA.Id, restoredB.Id) >= 1,
            "OPR-MUT-ADMIN-01 RESTORE_GREEN: production code must not allow the active-administrator count to reach zero.");
    }

    /// <summary>
    /// OPR-MUT-AUTH-01: authorization-state invalidation bypass. Semantic oracle: an old
    /// authenticated JWT remains authorized after a successful security-relevant mutation. This
    /// proof stays entirely on the mutation side: it swaps the production
    /// <see cref="IOperatorMutationSuccessCommitter"/> for a double that applies and persists the
    /// role change but reverts only the authorization-state version and security stamp fields
    /// before saving - a control that changed the role but forgot to invalidate prior
    /// authenticated sessions. <c>CurrentOperatorAuthorizationHandler</c> and AUTHZ policy are
    /// never touched. The mutation is a role promotion (Teller to Administrator) rather than a
    /// disable: <c>CurrentOperatorAuthorizationHandler</c> rejects a disabled Operator's token on
    /// the current-state check alone, before the version comparison is ever reached, which would
    /// mask this specific defect instead of isolating it.
    /// </summary>
    [Fact]
    public async Task OprMutAuth01OldAuthenticatedStateCannotSurviveSuccessfulMutation()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-auth01.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-auth01.target", OperatorRole.Teller);

        // BASELINE_GREEN: production commit path invalidates the pre-mutation token after a
        // successful role promotion.
        string baselineStaleToken = CreateToken(target);
        await using (OperatorMutationApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage mutation = await SendRoleChangeAsync(
                baselineClient,
                target.Id,
                CreateToken(actor),
                "opr-mut-auth01-baseline-mutation",
                "administrator");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            using HttpRequestMessage staleRequest = CreateQueryRequest(
                baselineStaleToken,
                "opr-mut-auth01-baseline-stale");
            using HttpResponseMessage staleResponse = await baselineClient.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, staleResponse.StatusCode);
        }

        await SetOperatorStateAsync(target.Id, OperatorState.Active, OperatorRole.Teller);
        Operator restoredActor = await ReadOperatorAsync(actor.Id);
        Operator restoredTarget = await ReadOperatorAsync(target.Id);

        // MUTATION_RED: the mutation-side commit path persists the role change and the required
        // success Audit, but reverts AuthorizationStateVersion/SecurityStamp to their pre-mutation
        // values before saving. The semantic failure is a 200 protected response from the
        // pre-mutation token after the mutation succeeds - AUTHZ itself is untouched, and the
        // probed endpoint only requires an authenticated current Operator (not a specific role),
        // so the outcome isolates the version/stamp bypass rather than a role-privilege check.
        string mutatedStaleToken = CreateToken(restoredTarget);
        await using (OperatorMutationApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationSuccessCommitter>();
            services.AddScoped<IOperatorMutationSuccessCommitter, AuthorizationInvalidationBypassSuccessCommitter>();
        }))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage mutation = await SendRoleChangeAsync(
                mutatedClient,
                target.Id,
                CreateToken(restoredActor),
                "opr-mut-auth01-mutated-mutation",
                "administrator");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            using HttpRequestMessage staleRequest = CreateQueryRequest(
                mutatedStaleToken,
                "opr-mut-auth01-mutated-stale");
            using HttpResponseMessage staleResponse = await mutatedClient.SendAsync(staleRequest);
            string body = await staleResponse.Content.ReadAsStringAsync();
            Assert.True(
                staleResponse.StatusCode == HttpStatusCode.OK,
                $"OPR-MUT-AUTH-01 MUTATION_RED: expected the pre-mutation token to remain authorized (200) after a successful mutation that bypassed authorization-state invalidation, but observed {(int)staleResponse.StatusCode}.");
            Assert.Contains("operatorIdentifier", body, StringComparison.Ordinal);
        }

        Assert.Equal(OperatorRole.Administrator, (await ReadOperatorAsync(target.Id)).Role);
        Assert.Single(await ReadAuditsAsync("opr-mut-auth01-mutated-mutation"));

        await SetOperatorStateAsync(target.Id, OperatorState.Active, OperatorRole.Teller);
        Operator finalActor = await ReadOperatorAsync(actor.Id);
        Operator finalTarget = await ReadOperatorAsync(target.Id);

        // RESTORE_GREEN.
        await using (OperatorMutationApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage mutation = await SendRoleChangeAsync(
                restoredClient,
                target.Id,
                CreateToken(finalActor),
                "opr-mut-auth01-restored-mutation",
                "administrator");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            using HttpRequestMessage staleRequest = CreateQueryRequest(
                CreateToken(finalTarget),
                "opr-mut-auth01-restored-stale");
            using HttpResponseMessage staleResponse = await restoredClient.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, staleResponse.StatusCode);
        }
    }

    [Fact]
    public async Task OprMutAud01RequiredSuccessAuditIsAtomicWithStateMutation()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-aud01.actor", OperatorRole.Administrator);

        // BASELINE_GREEN.
        Operator baselineTarget = await SeedOperatorAsync("opr-mut-aud01.baseline", OperatorRole.Viewer);
        CriticalAuditFailureProbe baselineProbe = new();
        await using (OperatorMutationApiFactory baseline = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(baselineProbe);
            services.AddScoped<IAuditWriter, FailingMutationAuditWriter>();
        }))
        using (HttpClient baselineClient = baseline.CreateClient())
        using (HttpResponseMessage baselineResponse = await SendDisableAsync(
                   baselineClient,
                   baselineTarget.Id,
                   CreateToken(actor),
                   "opr-mut-aud01-baseline"))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, baselineResponse.StatusCode);
        }

        Assert.Equal(OperatorState.Active, (await ReadOperatorAsync(baselineTarget.Id)).State);
        Assert.Equal(1, baselineProbe.CurrentTransactionInvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-mut-aud01-baseline"));

        // MUTATION_RED: committing state before the required Audit leaves a durable mutation
        // when the Audit fails. The oracle is the persisted disabled state with zero Audit rows.
        Operator mutatedTarget = await SeedOperatorAsync("opr-mut-aud01.mutated", OperatorRole.Viewer);
        CriticalAuditFailureProbe mutatedProbe = new();
        await using (OperatorMutationApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(mutatedProbe);
            services.AddScoped<IAuditWriter, FailingMutationAuditWriter>();
            services.RemoveAll<IOperatorMutationSuccessCommitter>();
            services.AddScoped<IOperatorMutationSuccessCommitter, NonAtomicMutationSuccessCommitter>();
        }))
        using (HttpClient mutatedClient = mutated.CreateClient())
        using (HttpResponseMessage mutatedResponse = await SendDisableAsync(
                   mutatedClient,
                   mutatedTarget.Id,
                   CreateToken(actor),
                   "opr-mut-aud01-mutated"))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, mutatedResponse.StatusCode);
        }

        Assert.Equal(OperatorState.Disabled, (await ReadOperatorAsync(mutatedTarget.Id)).State);
        Assert.Equal(0, mutatedProbe.CurrentTransactionInvocationCount);
        Assert.Equal(1, mutatedProbe.SeparateTransactionInvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-mut-aud01-mutated"));

        // RESTORE_GREEN.
        Operator restoredTarget = await SeedOperatorAsync("opr-mut-aud01.restored", OperatorRole.Viewer);
        CriticalAuditFailureProbe restoredProbe = new();
        await using (OperatorMutationApiFactory restored = CreateFactory(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton(restoredProbe);
            services.AddScoped<IAuditWriter, FailingMutationAuditWriter>();
        }))
        using (HttpClient restoredClient = restored.CreateClient())
        using (HttpResponseMessage restoredResponse = await SendDisableAsync(
                   restoredClient,
                   restoredTarget.Id,
                   CreateToken(actor),
                   "opr-mut-aud01-restored"))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, restoredResponse.StatusCode);
        }

        Assert.Equal(OperatorState.Active, (await ReadOperatorAsync(restoredTarget.Id)).State);
        Assert.Equal(1, restoredProbe.CurrentTransactionInvocationCount);
        Assert.Empty(await ReadAuditsAsync("opr-mut-aud01-restored"));
    }

    private OperatorMutationApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.Equal(MigratorExitCode.Success, run.ExitCode);
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await using BankDbContext context = CreateContext();
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<Operator> ReadOperatorAsync(Guid identifier)
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.SingleAsync(candidate => candidate.Id == identifier);
    }

    private BankDbContext CreateContext()
    {
        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        return new BankDbContext(options.Options);
    }

    private async Task SetOperatorStateAsync(Guid identifier, OperatorState state, OperatorRole role)
    {
        await using BankDbContext context = CreateContext();
        Operator entity = await context.Operators.SingleAsync(candidate => candidate.Id == identifier);
        entity.ApplyLifecycleMutation(
            state,
            role,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await context.SaveChangesAsync();
    }

    private async Task<long> CountActiveAdministratorsAsync()
    {
        await using BankDbContext context = CreateContext();
        return await context.Operators.LongCountAsync(candidate =>
            candidate.State == OperatorState.Active && candidate.Role == OperatorRole.Administrator);
    }

    private async Task<(Operator First, Operator Second)> SeedTwoActiveAdministratorsAsync(
        string firstUserName,
        string secondUserName)
    {
        Operator first = await SeedOperatorAsync(firstUserName, OperatorRole.Administrator);
        Operator second = await SeedOperatorAsync(secondUserName, OperatorRole.Administrator);
        return (first, second);
    }

    /// <summary>
    /// Disables every currently-active administrator via a direct write, bypassing the API. This
    /// test method's BASELINE_GREEN/MUTATION_RED/RESTORE_GREEN phases each seed their own pair of
    /// administrators into the same shared per-test-method database; without this reset, an
    /// earlier phase's surviving administrator would give a later phase's pair extra headroom and
    /// the active-administrator invariant would no longer bind on that pair alone.
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

    /// <summary>
    /// Counts active administrators restricted to a specific set of seeded identifiers, not the
    /// whole table - this test method's baseline/mutated/restored phases each seed their own
    /// administrators in the same shared database, so a global count would be polluted by other
    /// phases' surviving administrators.
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

    private async Task<IReadOnlyList<PersistedAudit>> ReadAuditsAsync(string correlationId)
    {
        List<PersistedAudit> audits = [];
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"SELECT {AuditPersistence.ActorIdentifierColumn}, {AuditPersistence.OperationIdentifierColumn}, {AuditPersistence.TargetIdentifierColumn}, {AuditPersistence.ResultColumn} FROM {AuditPersistence.TableName} WHERE {AuditPersistence.CorrelationIdColumn} = @correlation_id;",
            connection);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            audits.Add(new PersistedAudit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return audits;
    }

    private static async Task<HttpResponseMessage> SendDisableAsync(
        HttpClient client,
        Guid targetIdentifier,
        string token,
        string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{targetIdentifier:D}/disable");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRoleChangeAsync(
        HttpClient client,
        Guid targetIdentifier,
        string token,
        string correlationId,
        string role)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/operators/{targetIdentifier:D}/role");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = JsonContent.Create(new { role });
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateQueryRequest(string token, string correlationId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        return request;
    }

    private static string CreateToken(Operator operatorEntity)
    {
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            "minimal-bank-system",
            "minimal-bank-system-api",
            [
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
                    AuthnClaimTypes.AuthorizationStateVersion,
                    operatorEntity.AuthorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            ],
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record PersistedAudit(
        Guid ActorIdentifier,
        string OperationIdentifier,
        string TargetIdentifier,
        string Result);
}

/// <summary>
/// OPR-MUT-ADMIN-01 MUTATION_RED double: locks only the requested target row and derives the
/// active-administrator count from a plain unlocked snapshot read, instead of locking the full
/// active-administrator set the way the production <c>OperatorMutationService</c> does - the
/// exact "target Operatorだけをlockしてread-count-then-writeする実装では不十分です" pattern the
/// approved contract calls out by name as insufficient. Because the count is not backed by any
/// row lock, two concurrent requests each targeting a different one of the only two active
/// administrators can both observe the same stale count before either commits and both proceed.
/// Every other step (rejection rules, domain mutation, success-committer hand-off) mirrors
/// production so the double isolates the locking defect alone. Test-composition-only; never
/// referenced by production <c>Program.cs</c>.
/// </summary>
internal sealed class TargetOnlyUnlockedCountMutationService(
    BankDbContext persistence,
    IOperatorMutationSuccessCommitter successCommitter) : IOperatorMutationService
{
    public async Task<OperatorMutationResult> ExecuteAsync(
        Guid operatorIdentifier,
        OperatorMutationKind operation,
        OperatorRole? requestedRole,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        Operator? target = await persistence.Operators
            .FromSqlRaw(
                $"SELECT * FROM {OperatorPersistence.TableName} WHERE {OperatorPersistence.IdColumn} = {{0}} FOR UPDATE",
                operatorIdentifier)
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return OperatorMutationResult.Rejected(ApiErrorEnvelope.OperatorNotFound, StatusCodes.Status404NotFound);
        }

        // Deliberately unlocked: not serialized against a concurrent transaction running this
        // same insufficient strategy against the other administrator of the pair.
        long activeAdministratorCount = await persistence.Operators
            .AsNoTracking()
            .LongCountAsync(
                candidate => candidate.State == OperatorState.Active && candidate.Role == OperatorRole.Administrator,
                cancellationToken);

        OperatorState desiredState = operation == OperatorMutationKind.Enable
            ? OperatorState.Active
            : operation == OperatorMutationKind.Disable
                ? OperatorState.Disabled
                : target.State;
        OperatorRole desiredRole = operation == OperatorMutationKind.ChangeRole
            ? requestedRole ?? throw new InvalidOperationException("Role mutation requires a validated role.")
            : target.Role;

        ApiErrorEnvelope? rejection = GetRejection(
            target, operation, desiredState, desiredRole, actorIdentifier, activeAdministratorCount);
        if (rejection is not null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return OperatorMutationResult.Rejected(rejection, StatusCodes.Status409Conflict);
        }

        target.ApplyLifecycleMutation(
            desiredState,
            desiredRole,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await successCommitter
            .CommitAsync(target, operation, actorIdentifier, actorRole, httpContext, transaction, cancellationToken)
            .ConfigureAwait(false);

        return OperatorMutationResult.Succeeded(new OperatorMutationResponse(
            target.Id,
            target.State == OperatorState.Active ? OperatorPersistence.ActiveStateToken : OperatorPersistence.DisabledStateToken,
            ToRoleToken(target.Role)));
    }

    private static ApiErrorEnvelope? GetRejection(
        Operator target,
        OperatorMutationKind operation,
        OperatorState desiredState,
        OperatorRole desiredRole,
        Guid actorIdentifier,
        long activeAdministratorCount)
    {
        if (operation == OperatorMutationKind.Disable && actorIdentifier == target.Id)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        if (target.State == desiredState && target.Role == desiredRole)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        bool losesActiveAdministrator =
            target.State == OperatorState.Active &&
            target.Role == OperatorRole.Administrator &&
            (desiredState != OperatorState.Active || desiredRole != OperatorRole.Administrator);
        if (losesActiveAdministrator && activeAdministratorCount <= 1)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        return null;
    }

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Operator role."),
    };
}

/// <summary>
/// OPR-MUT-AUTH-01 MUTATION_RED double: applies and persists the state/role change and the
/// required success Audit, but reverts <see cref="Operator.AuthorizationStateVersion"/> and
/// <see cref="Operator.SecurityStamp"/> to their pre-mutation values (read from EF Core's
/// tracked <c>OriginalValues</c>, captured when the target was loaded, before the domain
/// mutation) before saving - modeling a control that changed state/role but forgot to invalidate
/// prior authenticated sessions. <c>CurrentOperatorAuthorizationHandler</c> and AUTHZ
/// policy are never touched. Test-composition-only; never referenced by production
/// <c>Program.cs</c>.
/// </summary>
internal sealed class AuthorizationInvalidationBypassSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationSuccessCommitter
{
    private static readonly PropertyInfo AuthorizationStateVersionProperty =
        typeof(Operator).GetProperty(nameof(Operator.AuthorizationStateVersion))!;

    private static readonly PropertyInfo SecurityStampProperty =
        typeof(Operator).GetProperty(nameof(Operator.SecurityStamp))!;

    public async Task CommitAsync(
        Operator target,
        OperatorMutationKind operation,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        object? originalVersion = persistence.Entry(target).OriginalValues[nameof(Operator.AuthorizationStateVersion)];
        object? originalStamp = persistence.Entry(target).OriginalValues[nameof(Operator.SecurityStamp)];

        AuthorizationStateVersionProperty.SetValue(target, originalVersion);
        SecurityStampProperty.SetValue(target, originalStamp);

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AuditWriteRequest successAudit = new(
            actorIdentifier,
            actorRole,
            OperatorMutationAudit.GetOperationIdentifier(operation),
            target.Id.ToString("D"),
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            httpContext.TraceIdentifier);

        await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class NonAtomicMutationSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        Operator target,
        OperatorMutationKind operation,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await persistence.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();

        await auditWriter.AppendInSeparateTransactionBeforeResultAsync(
            new AuditWriteRequest(
                actorIdentifier,
                actorRole,
                OperatorMutationAudit.GetOperationIdentifier(operation),
                target.Id.ToString("D"),
                AuditResult.Success,
                null,
                httpContext.TraceIdentifier),
            _ => Task.FromResult(true),
            cancellationToken);
    }
}

internal sealed class CriticalAuditFailureProbe
{
    private int currentTransactionInvocationCount;
    private int separateTransactionInvocationCount;

    public int CurrentTransactionInvocationCount => Volatile.Read(ref currentTransactionInvocationCount);

    public int SeparateTransactionInvocationCount => Volatile.Read(ref separateTransactionInvocationCount);

    public void RecordCurrentTransactionInvocation() => Interlocked.Increment(ref currentTransactionInvocationCount);

    public void RecordSeparateTransactionInvocation() => Interlocked.Increment(ref separateTransactionInvocationCount);
}

internal sealed class FailingMutationAuditWriter(CriticalAuditFailureProbe probe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        probe.RecordCurrentTransactionInvocation();
        throw new MutationCriticalAuditFailureException();
    }

    public Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = successResultFactory;
        _ = cancellationToken;
        probe.RecordSeparateTransactionInvocation();
        throw new MutationCriticalAuditFailureException();
    }
}

internal sealed class MutationCriticalAuditFailureException()
    : InvalidOperationException("Deterministic test-only OPR-MUT-AUD-01 Audit failure.");
