using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using MinimalBankSystem.IntegrationTests.Authorization;
using MinimalBankSystem.IntegrationTests.Persistence;
using MinimalBankSystem.Migrator;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// WP2-AUTHZ-01 (#168) Critical Mutations. Each mutation swaps the production
/// <see cref="CurrentOperatorAuthorizationHandler"/> for a narrow, single-check-removed variant
/// via DI substitution — a test-composition-only technique that never touches production
/// <c>Program.cs</c>. Each test proves BASELINE_GREEN → MUTATION_RED → RESTORE_GREEN against the
/// real ASP.NET Core authorization pipeline and a real persisted Operator, with an explicit
/// semantic failure signature (the previously rejected request reaches the protected handler).
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuthorizationCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "authz01-mutation-seed-password-not-for-production";
    private const string Issuer = "minimal-bank-system";
    private const string Audience = "minimal-bank-system-api";
    private static readonly DateTimeOffset FrozenUtc = new(2033, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public async Task AuthzState01DisabledCheckBypassIsKilled()
    {
        await MigrateAsync();
        Operator operatorRow = await SeedOperatorAsync("authz-state-01.disabled-bypass", OperatorRole.Administrator);
        string token = CreateToken(operatorRow);
        await UpdateOperatorAsync(operatorRow.Id, OperatorPersistence.DisabledStateToken);

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-01-baseline"),
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, baseline.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services =>
                   UseMutatedHandler<DisabledCheckBypassAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-01-mutated"),
                token);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-01: expected the disabled-Operator bypass to reach the handler. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
            Assert.Equal(1, mutated.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-01-restored"),
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, restored.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }
    }

    [Fact]
    public async Task AuthzState02AuthorizationStateVersionCheckBypassIsKilled()
    {
        await MigrateAsync();
        Operator operatorRow = await SeedOperatorAsync("authz-state-02.version-bypass", OperatorRole.Teller);
        string token = CreateToken(operatorRow);
        await BumpAuthorizationStateVersionAsync(operatorRow.Id);

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-02-baseline"),
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, baseline.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services =>
                   UseMutatedHandler<VersionCheckBypassAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-02-mutated"),
                token);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-02: expected the stale authorization-state-version bypass to reach the handler. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
            Assert.Equal(1, mutated.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient,
                Route(AuthorizationProbeController.FallbackRoute, "state-02-restored"),
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, restored.Services.GetRequiredService<AuthorizationProbeSignals>().FallbackHandlerReachCount);
        }
    }

    [Fact]
    public async Task AuthzState03JwtRoleClaimMadeAuthoritativeIsKilled()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz-state-03.role-claim-authoritative", OperatorRole.Viewer);
        string forgedToken = CreateToken(viewer, "Administrator");
        const string targetId = "authz-state-03-target";

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient,
                Route(AuthorizationProbeController.AdministratorRoute, targetId),
                forgedToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                0,
                baseline.Services.GetRequiredService<AuthorizationProbeSignals>().AdministratorHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services =>
                   UseMutatedHandler<RoleClaimAuthoritativeAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient,
                Route(AuthorizationProbeController.AdministratorRoute, targetId),
                forgedToken);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-03: expected the forged JWT role claim to reach the handler as Administrator. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
            Assert.Equal(
                1,
                mutated.Services.GetRequiredService<AuthorizationProbeSignals>().AdministratorHandlerReachCount);
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient,
                Route(AuthorizationProbeController.AdministratorRoute, targetId),
                forgedToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                0,
                restored.Services.GetRequiredService<AuthorizationProbeSignals>().AdministratorHandlerReachCount);
        }
    }

    private AuthorizationProbeApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(Database.ConnectionString, configureServices);

    private static void UseMutatedHandler<THandler>(IServiceCollection services)
        where THandler : class, IAuthorizationHandler
    {
        // Replace only the production current-Operator handler. Removing every
        // IAuthorizationHandler would also drop framework handlers such as
        // DenyAnonymousAuthorizationHandler required by RequireAuthenticatedUser().
        ServiceDescriptor[] productionHandlers = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IAuthorizationHandler) &&
                descriptor.ImplementationType == typeof(CurrentOperatorAuthorizationHandler))
            .ToArray();
        foreach (ServiceDescriptor descriptor in productionHandlers)
        {
            services.Remove(descriptor);
        }

        services.AddScoped<IAuthorizationHandler, THandler>();
    }

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(run.ExitCode == MigratorExitCode.Success, $"Expected AUTHZ mutation migration success. Output:\n{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            FrozenUtc,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task UpdateOperatorAsync(Guid operatorId, string stateToken)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = @state
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            connection);
        command.Parameters.AddWithValue("state", stateToken);
        command.Parameters.AddWithValue("id", operatorId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private async Task BumpAuthorizationStateVersionAsync(Guid operatorId)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.AuthorizationStateVersionColumn}
                 = {OperatorPersistence.AuthorizationStateVersionColumn} + 1
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            connection);
        command.Parameters.AddWithValue("id", operatorId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static string CreateToken(Operator operatorEntity, string? jwtRole = null) =>
        CreateToken(operatorEntity.Id, operatorEntity.AuthorizationStateVersion, jwtRole);

    private static string CreateToken(Guid operatorId, int authorizationStateVersion, string? jwtRole = null)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, operatorId.ToString("D")),
            new(
                AuthnClaimTypes.AuthorizationStateVersion,
                authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
        ];

        if (jwtRole is not null)
        {
            claims.Add(new Claim("role", jwtRole));
        }

        JwtSecurityToken token = new(
            Issuer,
            Audience,
            claims,
            now.AddMinutes(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string path, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string Route(string routeTemplate, string targetIdentifier) =>
        routeTemplate.Replace("{targetId}", targetIdentifier, StringComparison.Ordinal);
}

/// <summary>
/// AUTHZ-STATE-01 mutation: the active/disabled check is intentionally removed. Everything else
/// matches production CurrentOperatorAuthorizationHandler shape. Test composition only.
/// </summary>
internal sealed class DisabledCheckBypassAuthorizationHandler(
    CurrentOperatorRequestContext requestContext) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext authorizationContext)
    {
        if (!authorizationContext.Requirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or CurrentOperatorRoleRequirement) ||
            authorizationContext.Resource is not HttpContext httpContext ||
            authorizationContext.User.Identity?.IsAuthenticated != true ||
            httpContext.GetEndpoint() is not RouteEndpoint)
        {
            return;
        }

        if (!TryReadClaims(authorizationContext.User, out Guid operatorId, out int tokenVersion))
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        BankDbContext persistence = httpContext.RequestServices.GetRequiredService<BankDbContext>();
        CurrentOperatorSnapshot? currentOperator = await persistence.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorId)
            .Select(operatorEntity => new CurrentOperatorSnapshot(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.AuthorizationStateVersion))
            .SingleOrDefaultAsync(httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (currentOperator is null)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        // AUTHZ-STATE-01 mutation: the intended `currentOperator.State != OperatorState.Active`
        // check is deliberately absent here.

        if (currentOperator.AuthorizationStateVersion != tokenVersion)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        requestContext.SetCurrent(currentOperator);
        SucceedRequirements(authorizationContext, currentOperator.Role);
    }

    private static bool TryReadClaims(ClaimsPrincipal user, out Guid operatorId, out int version)
    {
        Claim[] subjectClaims = user.FindAll(JwtRegisteredClaimNames.Sub).ToArray();
        Claim[] versionClaims = user.FindAll(AuthnClaimTypes.AuthorizationStateVersion).ToArray();
        operatorId = default;
        version = default;
        return subjectClaims.Length == 1 &&
               Guid.TryParseExact(subjectClaims[0].Value, "D", out operatorId) &&
               versionClaims.Length == 1 &&
               int.TryParse(versionClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out version) &&
               version > 0;
    }

    private static void SucceedRequirements(AuthorizationHandlerContext context, OperatorRole role)
    {
        foreach (IAuthorizationRequirement requirement in context.Requirements)
        {
            switch (requirement)
            {
                case CurrentOperatorRequirement:
                    context.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement roleRequirement
                    when roleRequirement.PermittedRoles.Contains(role):
                    context.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement:
                    context.Fail();
                    break;
            }
        }
    }
}

/// <summary>
/// AUTHZ-STATE-02 mutation: the authorization-state-version comparison is intentionally removed.
/// </summary>
internal sealed class VersionCheckBypassAuthorizationHandler(
    CurrentOperatorRequestContext requestContext) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext authorizationContext)
    {
        if (!authorizationContext.Requirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or CurrentOperatorRoleRequirement) ||
            authorizationContext.Resource is not HttpContext httpContext ||
            authorizationContext.User.Identity?.IsAuthenticated != true ||
            httpContext.GetEndpoint() is not RouteEndpoint)
        {
            return;
        }

        Claim[] subjectClaims = authorizationContext.User.FindAll(JwtRegisteredClaimNames.Sub).ToArray();
        if (subjectClaims.Length != 1 ||
            !Guid.TryParseExact(subjectClaims[0].Value, "D", out Guid operatorId))
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        BankDbContext persistence = httpContext.RequestServices.GetRequiredService<BankDbContext>();
        CurrentOperatorSnapshot? currentOperator = await persistence.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorId)
            .Select(operatorEntity => new CurrentOperatorSnapshot(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.AuthorizationStateVersion))
            .SingleOrDefaultAsync(httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (currentOperator is null)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        if (currentOperator.State != OperatorState.Active)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        // AUTHZ-STATE-02 mutation: authorization-state-version comparison is deliberately absent.

        requestContext.SetCurrent(currentOperator);
        foreach (IAuthorizationRequirement requirement in authorizationContext.Requirements)
        {
            switch (requirement)
            {
                case CurrentOperatorRequirement:
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement roleRequirement
                    when roleRequirement.PermittedRoles.Contains(currentOperator.Role):
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement:
                    authorizationContext.Fail();
                    break;
            }
        }
    }
}

/// <summary>
/// AUTHZ-STATE-03 mutation: a JWT "role" claim, when present, is treated as authoritative instead
/// of the current database role.
/// </summary>
internal sealed class RoleClaimAuthoritativeAuthorizationHandler(
    CurrentOperatorRequestContext requestContext) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext authorizationContext)
    {
        if (!authorizationContext.Requirements.Any(requirement =>
                requirement is CurrentOperatorRequirement or CurrentOperatorRoleRequirement) ||
            authorizationContext.Resource is not HttpContext httpContext ||
            authorizationContext.User.Identity?.IsAuthenticated != true ||
            httpContext.GetEndpoint() is not RouteEndpoint)
        {
            return;
        }

        Claim[] subjectClaims = authorizationContext.User.FindAll(JwtRegisteredClaimNames.Sub).ToArray();
        Claim[] versionClaims = authorizationContext.User.FindAll(AuthnClaimTypes.AuthorizationStateVersion).ToArray();
        if (subjectClaims.Length != 1 ||
            !Guid.TryParseExact(subjectClaims[0].Value, "D", out Guid operatorId) ||
            versionClaims.Length != 1 ||
            !int.TryParse(
                versionClaims[0].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int tokenVersion) ||
            tokenVersion <= 0)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        BankDbContext persistence = httpContext.RequestServices.GetRequiredService<BankDbContext>();
        CurrentOperatorSnapshot? currentOperator = await persistence.Operators
            .AsNoTracking()
            .Where(operatorEntity => operatorEntity.Id == operatorId)
            .Select(operatorEntity => new CurrentOperatorSnapshot(
                operatorEntity.Id,
                operatorEntity.State,
                operatorEntity.Role,
                operatorEntity.AuthorizationStateVersion))
            .SingleOrDefaultAsync(httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (currentOperator is null)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        if (currentOperator.State != OperatorState.Active)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        if (currentOperator.AuthorizationStateVersion != tokenVersion)
        {
            requestContext.MarkAuthenticationInvalidated();
            return;
        }

        // AUTHZ-STATE-03 mutation: JWT role claim is treated as authoritative when present.
        string? roleClaim =
            authorizationContext.User.FindFirst("role")?.Value ??
            authorizationContext.User.FindFirst(ClaimTypes.Role)?.Value;
        OperatorRole effectiveRole = Enum.TryParse(roleClaim, ignoreCase: true, out OperatorRole claimedRole)
            ? claimedRole
            : currentOperator.Role;

        CurrentOperatorSnapshot authoritative = currentOperator with { Role = effectiveRole };
        requestContext.SetCurrent(authoritative);

        foreach (IAuthorizationRequirement requirement in authorizationContext.Requirements)
        {
            switch (requirement)
            {
                case CurrentOperatorRequirement:
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement roleRequirement
                    when roleRequirement.PermittedRoles.Contains(effectiveRole):
                    authorizationContext.Succeed(requirement);
                    break;
                case CurrentOperatorRoleRequirement:
                    authorizationContext.Fail();
                    break;
            }
        }
    }
}
