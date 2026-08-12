using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Infrastructure.Persistence;
using Npgsql;

namespace MinimalBankSystem.IntegrationTests;

/// <summary>
/// API-level evidence for the FND-06 operational health boundary. These tests intentionally need
/// no Docker: they establish that missing or unreachable dependencies remain operational 503s.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class HealthContractTests
{
    [Fact]
    public async Task LivenessSucceedsWithoutAConfiguredDatabase()
    {
        await using HealthApiFactory factory = new(connectionString: null);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.LivePath);

        await AssertLiveAsync(response);
    }

    [Fact]
    public async Task LivenessSucceedsWhenPostgreSqlIsUnreachable()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.LivePath);

        await AssertLiveAsync(response);
    }

    [Fact]
    public async Task LivenessNeverExecutesAReadinessTaggedCheck()
    {
        ReadinessSpy spy = new();
        await using HealthApiFactory factory = new(
            HealthConnectionStrings.Unreachable,
            services => services.AddHealthChecks().AddCheck(
                "fnd06-readiness-spy",
                spy,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthContract.ReadinessTag]));
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await AssertLiveAsync(live);
        }

        Assert.Equal(0, spy.InvocationCount);

        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            await AssertNotReadyAsync(ready);
        }

        Assert.Equal(1, spy.InvocationCount);
    }

    [Fact]
    public async Task ReadinessFailsWhenPostgreSqlIsUnreachable()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);

        await AssertNotReadyAsync(response);
    }

    [Fact]
    public async Task ReadinessFailsAsAnOperationalResponseWithoutAConnectionString()
    {
        await using HealthApiFactory factory = new(connectionString: null);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);

        await AssertNotReadyAsync(response);
    }

    [Fact]
    public async Task HealthFailureDoesNotDiscloseSensitiveValuesInTheResponseOrTechnicalLog()
    {
        using ConsoleCapture capture = new();

        await using (HealthApiFactory factory = new(HealthConnectionStrings.Unreachable))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath))
        {
            string body = await AssertNotReadyAsync(response);

            foreach (string prohibited in HealthConnectionStrings.ProhibitedDisclosures)
            {
                Assert.DoesNotContain(prohibited, body, StringComparison.OrdinalIgnoreCase);
            }
        }

        string logs = capture.Content;
        foreach (string prohibited in HealthConnectionStrings.ProhibitedLogDisclosures)
        {
            Assert.DoesNotContain(prohibited, logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(false, "database_unreachable")]
    [InlineData(true, "dependency_failure")]
    public async Task ReadinessFailureIsTechnicallyLoggedButNotReturned(
        bool withoutConnectionString,
        string expectedReason)
    {
        using ConsoleCapture capture = new();

        await using (HealthApiFactory factory = new(
                         withoutConnectionString ? null : HealthConnectionStrings.Unreachable))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath))
        {
            string body = await AssertNotReadyAsync(response);
            Assert.DoesNotContain(expectedReason, body, StringComparison.Ordinal);
        }

        Assert.Contains(expectedReason, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpointsKeepTheExistingCorrelationIdContract()
    {
        const string correlationId = "health-correlation_01.safe";
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        foreach (string path in new[] { HealthContract.LivePath, HealthContract.ReadyPath })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(
                correlationId,
                Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        }
    }

    [Fact]
    public async Task HealthEndpointsAreGetOnlyAndNonGetCannotSucceedAsHealth()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, HealthContract.LivePath);

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.NotEqual(HealthContract.HealthyBody, body);
        Assert.NotEqual(HealthContract.UnhealthyBody, body);
    }

    [Fact]
    public async Task UnknownHealthPathStillUsesTheFnd02NotFoundEnvelope()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/does-not-exist");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("\"code\":\"endpoint_not_found\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthContractHoldsOverRealKestrelTransport()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        factory.UseKestrel(0);
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await AssertLiveAsync(live);
        }

        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            await AssertNotReadyAsync(ready);
        }
    }

    internal static async Task AssertLiveAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthContract.HealthyBody, await response.Content.ReadAsStringAsync());
        AssertSanitizedPlainText(response);
    }

    internal static async Task AssertReadyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthContract.HealthyBody, await response.Content.ReadAsStringAsync());
        AssertSanitizedPlainText(response);
    }

    internal static async Task<string> AssertNotReadyAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthContract.UnhealthyBody, body);
        AssertSanitizedPlainText(response);
        Assert.DoesNotContain("\"code\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"message\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_error", body, StringComparison.Ordinal);

        return body;
    }

    private static void AssertSanitizedPlainText(HttpResponseMessage response)
    {
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    private sealed class ReadinessSpy : IHealthCheck
    {
        private int invocationCount;

        public int InvocationCount => Volatile.Read(ref invocationCount);

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocationCount);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}

internal static class HealthConnectionStrings
{
    public const string PasswordSentinel = "FND06_PASSWORD_SENTINEL_9F2C41";
    public const string UsernameSentinel = "fnd06_user_sentinel_9f2c41";
    public const string DatabaseSentinel = "fnd06_database_sentinel_9f2c41";

    public static string Unreachable { get; } = new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1",
        Port = 1,
        Database = DatabaseSentinel,
        Username = UsernameSentinel,
        Password = PasswordSentinel,
        Pooling = false,
        Timeout = 2,
        CommandTimeout = 2,
    }.ConnectionString;

    public static string[] ProhibitedDisclosures { get; } =
    [
        PasswordSentinel,
        UsernameSentinel,
        DatabaseSentinel,
        "Password=",
        "Host=",
        "127.0.0.1",
        "Exception",
        "stack",
    ];

    public static string[] ProhibitedLogDisclosures { get; } =
    [
        PasswordSentinel,
        UsernameSentinel,
        DatabaseSentinel,
        "Password=",
        "ConnectionStrings__",
        "stack trace",
        "   at ",
    ];
}

internal sealed class HealthApiFactory(
    string? connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting($"ConnectionStrings:{BankPersistence.ConnectionStringName}", connectionString ?? string.Empty);

        if (configureServices is not null)
        {
            builder.ConfigureServices(configureServices);
        }
    }
}
