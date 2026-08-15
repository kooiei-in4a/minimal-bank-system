using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Authentication;
using MinimalBankSystem.Infrastructure.Authorization;
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
/// via DI substitution — a test-composition-only technique, analogous to AUD-01's
/// deterministic-failure-injection interceptor, that never touches production <c>Program.cs</c>.
/// Each test proves the full BASELINE_GREEN -&gt; MUTATION_APPLIED -&gt; MUTATION_RED -&gt;
/// RESTORE -&gt; RESTORE_GREEN sequence against the real ASP.NET Core authorization pipeline and a
/// real persisted Operator, with an explicit semantic failure signature (the previously-rejected
/// request now reaches the protected handler) rather than a generic test RED.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
public sealed class AuthorizationCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "authz01-mutation-seed-password-not-for-production";
    private const string Issuer = "minimal-bank-system";
    private const string Audience = "minimal-bank-system-api";

    [Fact]
    public async Task AuthzState01DisabledCheckBypassIsKilled()
    {
        await MigrateAsync();
        Operator operatorRow = await SeedOperatorAsync("authz-state-01.disabled-bypass", OperatorRole.Administrator);
        string token = await LoginAndCaptureTokenAsync(operatorRow.UserName);
        await SetOperatorStateAsync(operatorRow.Id, OperatorPersistence.DisabledStateToken);

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services => UseMutatedHandler<DisabledCheckBypassAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-01: expected the disabled-Operator bypass to reach the handler. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task AuthzState02AuthorizationStateVersionCheckBypassIsKilled()
    {
        await MigrateAsync();
        Operator operatorRow = await SeedOperatorAsync("authz-state-02.version-bypass", OperatorRole.Teller);
        string token = await LoginAndCaptureTokenAsync(operatorRow.UserName);
        await BumpAuthorizationStateVersionAsync(operatorRow.Id);

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services => UseMutatedHandler<VersionCheckBypassAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-02: expected the stale authorization-state-version bypass to reach the handler. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient, AuthorizationProbeController.AnyCurrentOperatorPath, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task AuthzState03JwtRoleClaimMadeAuthoritativeIsKilled()
    {
        await MigrateAsync();
        Operator viewer = await SeedOperatorAsync("authz-state-03.role-claim-authoritative", OperatorRole.Viewer);
        string forgedToken = CreateToken(
            viewer.Id,
            Operator.InitialAuthorizationStateVersion,
            [new Claim("role", nameof(OperatorRole.Administrator))]);
        const string targetId = "authz-state-03-target";

        using (AuthorizationProbeApiFactory baseline = CreateFactory())
        using (HttpClient baselineClient = baseline.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                baselineClient, AdministratorOnlyPath(targetId), forgedToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (AuthorizationProbeApiFactory mutated = CreateFactory(services => UseMutatedHandler<RoleClaimAuthoritativeAuthorizationHandler>(services)))
        using (HttpClient mutatedClient = mutated.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                mutatedClient, AdministratorOnlyPath(targetId), forgedToken);
            string body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"AUTHZ-STATE-03: expected the forged JWT role claim to reach the handler as Administrator. Status: {response.StatusCode}. Body: {body}");
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("handlerReached").GetBoolean());
        }

        using (AuthorizationProbeApiFactory restored = CreateFactory())
        using (HttpClient restoredClient = restored.CreateClient())
        {
            using HttpResponseMessage response = await GetWithBearerAsync(
                restoredClient, AdministratorOnlyPath(targetId), forgedToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static string AdministratorOnlyPath(string targetId) => $"/__authz-probe/administrator-only/{targetId}";

    private AuthorizationProbeApiFactory CreateFactory(Action<IServiceCollection>? configureServices = null) =>
        new(TestJwtConfiguration.SigningKey, Database.ConnectionString, configureServices);

    private static void UseMutatedHandler<THandler>(IServiceCollection services)
        where THandler : class, IAuthorizationHandler
    {
        services.RemoveAll<IAuthorizationHandler>();
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
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString());

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();

        return created;
    }

    private async Task<string> LoginAndCaptureTokenAsync(string userName)
    {
        using AuthorizationProbeApiFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName, password = SeedPlaintextPassword });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected a successful AUTHN login for '{userName}'. Status: {response.StatusCode}. Body: {body}");

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string path, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private Task SetOperatorStateAsync(Guid operatorId, string stateToken) =>
        ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.StateColumn} = @state
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("state", stateToken),
            ("id", operatorId));

    private Task BumpAuthorizationStateVersionAsync(Guid operatorId) =>
        ExecuteNonQueryAsync(
            $"""
             UPDATE {OperatorPersistence.TableName}
             SET {OperatorPersistence.AuthorizationStateVersionColumn}
                 = {OperatorPersistence.AuthorizationStateVersionColumn} + 1
             WHERE {OperatorPersistence.IdColumn} = @id;
             """,
            ("id", operatorId));

    private async Task ExecuteNonQueryAsync(string commandText, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static string CreateToken(Guid subject, int authorizationStateVersion, IEnumerable<Claim> extraClaims)
    {
        DateTime now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, subject.ToString("D")),
            new Claim(
                AuthnClaimTypes.AuthorizationStateVersion,
                authorizationStateVersion.ToString(CultureInfo.InvariantCulture)),
            .. extraClaims,
        ];

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
}

/// <summary>
/// AUTHZ-STATE-01 mutation: the active/disabled check is intentionally removed. Everything else
/// matches production <see cref="CurrentOperatorAuthorizationHandler"/>. Test composition only;
/// never referenced by production code.
/// </summary>
internal sealed class DisabledCheckBypassAuthorizationHandler(IServiceProvider serviceProvider)
    : AuthorizationHandler<CurrentOperatorAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentOperatorAuthorizationRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail();
            return;
        }

        Endpoint? endpoint = httpContext.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        if (!TryReadClaims(context.User, out Guid operatorId, out int tokenVersion))
        {
            context.Fail();
            return;
        }

        BankDbContext dbContext = serviceProvider.GetRequiredService<BankDbContext>();
        Operator? current = await dbContext.Operators.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == operatorId, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (current is null)
        {
            context.Fail();
            return;
        }

        // AUTHZ-STATE-01 mutation: the intended `current.State is not OperatorState.Active` check
        // is deliberately absent here.

        if (current.AuthorizationStateVersion != tokenVersion)
        {
            context.Fail();
            return;
        }

        if (requirement.AllowedRoles.Count > 0 && !requirement.AllowedRoles.Contains(current.Role))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }

    private static bool TryReadClaims(System.Security.Claims.ClaimsPrincipal user, out Guid operatorId, out int version)
    {
        string? subjectClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        string? versionClaim = user.FindFirst(AuthnClaimTypes.AuthorizationStateVersion)?.Value;
        bool subjectParsed = Guid.TryParse(subjectClaim, out operatorId);
        bool versionParsed = int.TryParse(versionClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out version);
        return subjectParsed && versionParsed;
    }
}

/// <summary>
/// AUTHZ-STATE-02 mutation: the authorization-state-version comparison is intentionally removed.
/// Everything else matches production <see cref="CurrentOperatorAuthorizationHandler"/>. Test
/// composition only; never referenced by production code.
/// </summary>
internal sealed class VersionCheckBypassAuthorizationHandler(IServiceProvider serviceProvider)
    : AuthorizationHandler<CurrentOperatorAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentOperatorAuthorizationRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail();
            return;
        }

        Endpoint? endpoint = httpContext.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        string? subjectClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subjectClaim, out Guid operatorId))
        {
            context.Fail();
            return;
        }

        BankDbContext dbContext = serviceProvider.GetRequiredService<BankDbContext>();
        Operator? current = await dbContext.Operators.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == operatorId, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (current is null)
        {
            context.Fail();
            return;
        }

        if (current.State is not OperatorState.Active)
        {
            context.Fail();
            return;
        }

        // AUTHZ-STATE-02 mutation: the intended authorization-state-version comparison against the
        // token's claim is deliberately absent here.

        if (requirement.AllowedRoles.Count > 0 && !requirement.AllowedRoles.Contains(current.Role))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}

/// <summary>
/// AUTHZ-STATE-03 mutation: a JWT "role" claim, when present, is treated as authoritative for the
/// role check instead of the current database role. Everything else matches production
/// <see cref="CurrentOperatorAuthorizationHandler"/>. Test composition only; never referenced by
/// production code.
/// </summary>
internal sealed class RoleClaimAuthoritativeAuthorizationHandler(IServiceProvider serviceProvider)
    : AuthorizationHandler<CurrentOperatorAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentOperatorAuthorizationRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail();
            return;
        }

        Endpoint? endpoint = httpContext.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        string? subjectClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        string? versionClaim = context.User.FindFirst(AuthnClaimTypes.AuthorizationStateVersion)?.Value;

        if (!Guid.TryParse(subjectClaim, out Guid operatorId) ||
            !int.TryParse(versionClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tokenVersion))
        {
            context.Fail();
            return;
        }

        BankDbContext dbContext = serviceProvider.GetRequiredService<BankDbContext>();
        Operator? current = await dbContext.Operators.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == operatorId, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (current is null)
        {
            context.Fail();
            return;
        }

        if (current.State is not OperatorState.Active)
        {
            context.Fail();
            return;
        }

        if (current.AuthorizationStateVersion != tokenVersion)
        {
            context.Fail();
            return;
        }

        // AUTHZ-STATE-03 mutation: a JWT role claim, when present and parseable, is treated as
        // authoritative here instead of the current database role.
        string? roleClaim = context.User.FindFirst("role")?.Value;
        OperatorRole effectiveRole = Enum.TryParse(roleClaim, ignoreCase: true, out OperatorRole claimedRole)
            ? claimedRole
            : current.Role;

        if (requirement.AllowedRoles.Count > 0 && !requirement.AllowedRoles.Contains(effectiveRole))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}
