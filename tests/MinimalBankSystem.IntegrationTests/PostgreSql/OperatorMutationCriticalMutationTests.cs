extern alias api;

using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Authorization;
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

    [Fact]
    public async Task OprMutAdmin01ActiveAdministratorInvariantIsProvenWithSemanticOracle()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-admin01.actor", OperatorRole.Administrator);
        string baselineToken = CreateToken(actor);

        // BASELINE_GREEN: the production service rejects disabling the only active administrator.
        await using (OperatorMutationApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        using (HttpResponseMessage baselineResponse = await SendDisableAsync(
                   baselineClient,
                   actor.Id,
                   baselineToken,
                   "opr-mut-admin01-baseline"))
        {
            Assert.Equal(HttpStatusCode.Conflict, baselineResponse.StatusCode);
            Assert.Contains(
                "state_transition_not_allowed",
                await baselineResponse.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        Assert.Equal(1L, await CountActiveAdministratorsAsync());

        // MUTATION_RED: a test-composition-only service removes the invariant check. The oracle
        // is the committed active-administrator count, not merely the HTTP success status.
        await using (OperatorMutationApiFactory mutated = CreateFactory(services =>
        {
            services.RemoveAll<IOperatorMutationService>();
            services.AddScoped<IOperatorMutationService, LastAdministratorBypassMutationService>();
        }))
        using (HttpClient mutatedClient = mutated.CreateClient())
        using (HttpResponseMessage mutatedResponse = await SendDisableAsync(
                   mutatedClient,
                   actor.Id,
                   baselineToken,
                   "opr-mut-admin01-mutated"))
        {
            Assert.Equal(HttpStatusCode.OK, mutatedResponse.StatusCode);
        }

        Assert.Equal(0L, await CountActiveAdministratorsAsync());

        await SetOperatorStateAsync(actor.Id, OperatorState.Active, OperatorRole.Administrator);
        Operator restoredActor = await ReadOperatorAsync(actor.Id);

        // RESTORE_GREEN: production behavior again rejects the same semantic violation.
        await using (OperatorMutationApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        using (HttpResponseMessage restoredResponse = await SendDisableAsync(
                   restoredClient,
                   actor.Id,
                   CreateToken(restoredActor),
                   "opr-mut-admin01-restored"))
        {
            Assert.Equal(HttpStatusCode.Conflict, restoredResponse.StatusCode);
            Assert.Contains(
                "state_transition_not_allowed",
                await restoredResponse.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        Assert.Equal(1L, await CountActiveAdministratorsAsync());
    }

    [Fact]
    public async Task OprMutAuth01OldAuthenticatedStateCannotSurviveSuccessfulMutation()
    {
        await MigrateAsync();
        Operator actor = await SeedOperatorAsync("opr-mut-auth01.actor", OperatorRole.Administrator);
        Operator target = await SeedOperatorAsync("opr-mut-auth01.target", OperatorRole.Administrator);

        // BASELINE_GREEN: successful disable bumps the version and the old target JWT is rejected
        // before the protected Operator query handler is reached.
        string baselineStaleToken = CreateToken(target);
        await using (OperatorMutationApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage mutation = await SendDisableAsync(
                baselineClient,
                target.Id,
                CreateToken(actor),
                "opr-mut-auth01-baseline-mutation");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            using HttpRequestMessage staleRequest = CreateQueryRequest(
                baselineStaleToken,
                "opr-mut-auth01-baseline-stale");
            using HttpResponseMessage staleResponse = await baselineClient.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, staleResponse.StatusCode);
        }

        await SetOperatorStateAsync(target.Id, OperatorState.Active, OperatorRole.Administrator);
        Operator restoredActor = await ReadOperatorAsync(actor.Id);
        Operator restoredTarget = await ReadOperatorAsync(target.Id);

        // MUTATION_RED: test-only handler omits both current-state and version checks. The semantic
        // failure is a 200 protected response from an old token after the mutation succeeds.
        string mutatedStaleToken = CreateToken(restoredTarget);
        await using (OperatorMutationApiFactory mutated = CreateFactory(services =>
        {
            ServiceDescriptor[] productionHandlers = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IAuthorizationHandler) &&
                    descriptor.ImplementationType == typeof(CurrentOperatorAuthorizationHandler))
                .ToArray();
            foreach (ServiceDescriptor descriptor in productionHandlers)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IAuthorizationHandler, AuthorizationStateAndVersionBypassHandler>();
        }))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage mutation = await SendDisableAsync(
                mutatedClient,
                target.Id,
                CreateToken(restoredActor),
                "opr-mut-auth01-mutated-mutation");
            Assert.Equal(HttpStatusCode.OK, mutation.StatusCode);

            using HttpRequestMessage staleRequest = CreateQueryRequest(
                mutatedStaleToken,
                "opr-mut-auth01-mutated-stale");
            using HttpResponseMessage staleResponse = await mutatedClient.SendAsync(staleRequest);
            string body = await staleResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
            Assert.Contains("operatorIdentifier", body, StringComparison.Ordinal);
        }

        await SetOperatorStateAsync(target.Id, OperatorState.Active, OperatorRole.Administrator);
        Operator finalActor = await ReadOperatorAsync(actor.Id);
        Operator finalTarget = await ReadOperatorAsync(target.Id);

        // RESTORE_GREEN.
        await using (OperatorMutationApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage mutation = await SendDisableAsync(
                restoredClient,
                target.Id,
                CreateToken(finalActor),
                "opr-mut-auth01-restored-mutation");
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

internal sealed class LastAdministratorBypassMutationService(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationService
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
        Operator target = await persistence.Operators.SingleAsync(
            candidate => candidate.Id == operatorIdentifier,
            cancellationToken);
        OperatorRole role = requestedRole ?? target.Role;
        OperatorState state = operation == OperatorMutationKind.Disable
            ? OperatorState.Disabled
            : target.State;
        target.ApplyLifecycleMutation(
            state,
            role,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await persistence.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendToCurrentTransactionAsync(
            new AuditWriteRequest(
                actorIdentifier,
                actorRole,
                "operator.command.disable",
                target.Id.ToString("D"),
                AuditResult.Success,
                null,
                httpContext.TraceIdentifier),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperatorMutationResult.Succeeded(
            new OperatorMutationResponse(target.Id, "disabled", OperatorPersistence.AdministratorRoleToken));
    }
}

internal sealed class AuthorizationStateAndVersionBypassHandler(
    CurrentOperatorRequestContext requestContext) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext authorizationContext)
    {
        if (!authorizationContext.Requirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or CurrentOperatorRoleRequirement) ||
            authorizationContext.Resource is not HttpContext httpContext ||
            httpContext.User.Identity?.IsAuthenticated != true ||
            httpContext.GetEndpoint() is not RouteEndpoint)
        {
            return;
        }

        Claim? subject = authorizationContext.User.FindFirst(JwtRegisteredClaimNames.Sub);
        if (subject is null || !Guid.TryParseExact(subject.Value, "D", out Guid operatorIdentifier))
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        BankDbContext persistence = httpContext.RequestServices.GetRequiredService<BankDbContext>();
        CurrentOperatorSnapshot? current = await persistence.Operators
            .AsNoTracking()
            .Where(candidate => candidate.Id == operatorIdentifier)
            .Select(candidate => new CurrentOperatorSnapshot(
                candidate.Id,
                candidate.State,
                candidate.Role,
                candidate.AuthorizationStateVersion))
            .SingleOrDefaultAsync(httpContext.RequestAborted);
        if (current is null)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        // OPR-MUT-AUTH-01 MUTATION_RED: current state and authorization-state version are both
        // ignored, allowing the old JWT to remain authorized after a successful mutation.
        requestContext.SetCurrent(current);
        foreach (IAuthorizationRequirement requirement in authorizationContext.Requirements)
        {
            switch (requirement)
            {
                case CurrentOperatorRequirement:
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement roleRequirement
                    when roleRequirement.PermittedRoles.Contains(current.Role):
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement:
                    authorizationContext.Fail();
                    break;
            }
        }
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
