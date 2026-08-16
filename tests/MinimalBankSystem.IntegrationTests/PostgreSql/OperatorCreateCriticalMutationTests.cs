extern alias api;

using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MinimalBankSystem.Api.OperatorCreate;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
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
/// OPR-CREATE-AUD-01 (Issue #170) Critical Mutation. The mutation swaps the production
/// <see cref="IOperatorCreateSuccessCommitter"/> for a narrow, deliberately non-atomic test
/// double via DI substitution — a test-composition-only technique matching the approach already
/// established by WP2-AUTHZ-01's Critical Mutation tests
/// (<see cref="AuthorizationCriticalMutationTests"/>). It proves BASELINE_GREEN -&gt;
/// MUTATION_RED -&gt; RESTORE_GREEN against the real ASP.NET Core pipeline and a real persisted
/// Operator, with an explicit semantic failure signature: a committed Operator row that exists
/// without the required success Audit — not merely a failing HTTP status or a generic exception.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class OperatorCreateCriticalMutationTests(PostgreSqlContainerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture), IClassFixture<PostgreSqlContainerFixture>
{
    private const string SeedPlaintextPassword = "oprcreate-aud01-seed-password-not-for-production";

    [Fact]
    public async Task OprCreateAud01RequiredSuccessAuditAtomicityBypassIsKilled()
    {
        await MigrateAsync();
        Operator administrator = await SeedOperatorAsync("oprcreate-aud01.admin", OperatorRole.Administrator);
        string token = CreateToken(administrator);

        // BASELINE_GREEN: production IOperatorCreateSuccessCommitter + a required-Audit failure
        // injected on the success path. The Operator insert and the required success Audit share
        // one caller-owned transaction, so the failed Audit rolls the Operator insert back too.
        const string baselineLogin = "oprcreate-aud01.baseline";
        const string baselineNormalized = "OPRCREATE-AUD01.BASELINE";
        using (AuditFailureProbe baselineProbe = new())
        {
            await using OperatorCreateAud01ApiFactory baseline = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(baselineProbe);
                services.AddScoped<IAuditWriter, RequiredSuccessAuditFailureWriter>();
            });
            using HttpClient baselineClient = baseline.CreateClient();
            using HttpResponseMessage baselineResponse = await SendCreateAsync(
                baselineClient, token, "oprcreate-aud01-baseline", baselineLogin, "Baseline-Password-1!", "teller");

            Assert.Equal(HttpStatusCode.InternalServerError, baselineResponse.StatusCode);
            Assert.Equal(1, baselineProbe.InvocationCount);
        }

        Assert.False(
            await OperatorExistsAsync(baselineNormalized),
            "OPR-CREATE-AUD-01 BASELINE_GREEN: production code unexpectedly left a committed Operator row after a required success Audit failure.");
        Assert.Equal(0L, await CountAuditsAsync("oprcreate-aud01-baseline"));

        // MUTATION_RED: the same required-Audit failure, but the success commit path is replaced
        // with a non-atomic double that persists the Operator row in its own implicit transaction
        // BEFORE attempting the (failing) required Audit write. This models loss/bypass of the
        // create-success Audit atomicity control described by OPR-CREATE-AUD-01.
        const string mutatedLogin = "oprcreate-aud01.mutated";
        const string mutatedNormalized = "OPRCREATE-AUD01.MUTATED";
        using (AuditFailureProbe mutatedProbe = new())
        {
            await using OperatorCreateAud01ApiFactory mutated = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(mutatedProbe);
                services.AddScoped<IAuditWriter, RequiredSuccessAuditFailureWriter>();
                services.RemoveAll<IOperatorCreateSuccessCommitter>();
                services.AddScoped<IOperatorCreateSuccessCommitter, NonAtomicOperatorCreateSuccessCommitter>();
            });
            using HttpClient mutatedClient = mutated.CreateClient();
            using HttpResponseMessage mutatedResponse = await SendCreateAsync(
                mutatedClient, token, "oprcreate-aud01-mutated", mutatedLogin, "Mutated-Password-1!", "teller");

            // The client-observable outcome still looks like a safe failure...
            Assert.Equal(HttpStatusCode.InternalServerError, mutatedResponse.StatusCode);
            Assert.Equal(1, mutatedProbe.InvocationCount);
        }

        // ...but the semantic invariant is broken: the mutated commit path already committed the
        // Operator row in its own transaction before the required Audit write failed.
        bool mutatedOperatorExists = await OperatorExistsAsync(mutatedNormalized);
        long mutatedSuccessAuditCount = await CountAuditsAsync("oprcreate-aud01-mutated");
        Assert.True(
            mutatedOperatorExists,
            "OPR-CREATE-AUD-01 MUTATION_RED: expected the non-atomic commit path to leave a committed Operator row despite the required success Audit failing to persist.");
        Assert.Equal(0L, mutatedSuccessAuditCount);

        // RESTORE_GREEN: back to the production commit path with the same Audit failure. The
        // atomicity invariant holds again.
        const string restoredLogin = "oprcreate-aud01.restored";
        const string restoredNormalized = "OPRCREATE-AUD01.RESTORED";
        using (AuditFailureProbe restoredProbe = new())
        {
            await using OperatorCreateAud01ApiFactory restored = CreateFactory(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton(restoredProbe);
                services.AddScoped<IAuditWriter, RequiredSuccessAuditFailureWriter>();
            });
            using HttpClient restoredClient = restored.CreateClient();
            using HttpResponseMessage restoredResponse = await SendCreateAsync(
                restoredClient, token, "oprcreate-aud01-restored", restoredLogin, "Restored-Password-1!", "teller");

            Assert.Equal(HttpStatusCode.InternalServerError, restoredResponse.StatusCode);
            Assert.Equal(1, restoredProbe.InvocationCount);
        }

        Assert.False(
            await OperatorExistsAsync(restoredNormalized),
            "OPR-CREATE-AUD-01 RESTORE_GREEN: production code must not leave a committed Operator row after a required success Audit failure.");
        Assert.Equal(0L, await CountAuditsAsync("oprcreate-aud01-restored"));
    }

    private OperatorCreateAud01ApiFactory CreateFactory(Action<IServiceCollection> configureServices) =>
        new(Database.ConnectionString, configureServices);

    private async Task MigrateAsync()
    {
        MigratorRun run = await MigratorProcess.RunAsync(Database.ConnectionString, TimeSpan.FromSeconds(120));
        Assert.True(
            run.ExitCode == MigratorExitCode.Success,
            $"Expected OPR-CREATE-AUD-01 mutation migration success. Output:{Environment.NewLine}{run.Output}");
    }

    private async Task<Operator> SeedOperatorAsync(string userName, OperatorRole role)
    {
        Operator created = OperatorFactory.Create(
            userName,
            SeedPlaintextPassword,
            role,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        DbContextOptionsBuilder<BankDbContext> options = new();
        options.UseBankPostgreSql(Database.ConnectionString, BankPersistence.MigrationTimeoutSeconds);
        await using BankDbContext context = new(options.Options);
        context.Operators.Add(created);
        await context.SaveChangesAsync();
        return created;
    }

    private async Task<bool> OperatorExistsAsync(string normalizedUserName)
    {
        await using NpgsqlConnection connection = new(Database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"""
             SELECT EXISTS (
                 SELECT 1 FROM {OperatorPersistence.TableName}
                 WHERE {OperatorPersistence.NormalizedUserNameColumn} = @normalized_user_name);
             """,
            connection);
        command.Parameters.AddWithValue("normalized_user_name", normalizedUserName);
        return (bool)(await command.ExecuteScalarAsync())!;
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

    private static async Task<HttpResponseMessage> SendCreateAsync(
        HttpClient client,
        string token,
        string correlationId,
        string loginIdentifier,
        string password,
        string role)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/operators");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        request.Content = JsonContent.Create(new { loginIdentifier, password, role });
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
                new Claim(JwtRegisteredClaimNames.Sub, operatorEntity.Id.ToString("D")),
                new Claim(
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

internal sealed class OperatorCreateAud01ApiFactory(
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

internal sealed class AuditFailureProbe : IDisposable
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void RecordInvocation() => Interlocked.Increment(ref invocationCount);

    public void Dispose()
    {
    }
}

/// <summary>
/// Fails only the required create-success Audit primitive
/// (<see cref="IAuditWriter.AppendToCurrentTransactionAsync"/>), matching how both the production
/// and mutated commit paths attempt to persist the success Audit. Handler-rejection Audits (via
/// <see cref="IAuditWriter.AppendInSeparateTransactionBeforeResultAsync{TResult}"/>) are not
/// exercised by this Critical Mutation and are left supported so an unrelated rejection cannot be
/// silently miscounted as this failure.
/// </summary>
internal sealed class RequiredSuccessAuditFailureWriter(AuditFailureProbe failureProbe) : IAuditWriter
{
    public Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        failureProbe.RecordInvocation();
        throw new OperatorCreateAud01FailureInjectionException();
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
        throw new OperatorCreateAud01FailureInjectionException();
    }
}

internal sealed class OperatorCreateAud01FailureInjectionException()
    : InvalidOperationException("Deterministic test-only OPR-CREATE-AUD-01 required success Audit failure.");

/// <summary>
/// OPR-CREATE-AUD-01 MUTATION_RED double: the Operator insert commits in its own implicit
/// transaction, independent of the required success Audit write that follows. If that Audit
/// write fails, the Operator row remains committed — the exact atomicity/control loss the
/// mutation must model. Test-composition-only; never referenced by production Program.cs.
/// </summary>
internal sealed class NonAtomicOperatorCreateSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorCreateSuccessCommitter
{
    public async Task CommitAsync(
        Operator newOperator,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        persistence.Operators.Add(newOperator);
        // No caller-owned transaction: EF Core commits this SaveChangesAsync call immediately and
        // independently of the Audit write below.
        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AuditWriteRequest successAudit = new(
            actorIdentifier,
            actorRole,
            OperatorCreateAudit.OperationIdentifier,
            newOperator.Id.ToString("D"),
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            httpContext.TraceIdentifier);

        await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                successAudit,
                _ => Task.FromResult(true),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
