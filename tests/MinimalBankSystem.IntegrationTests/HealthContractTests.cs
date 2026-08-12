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
/// FND-06 health contract evidence that does not need a running PostgreSQL server: liveness must
/// stay independent of PostgreSQL, and readiness must fail as an operational health failure
/// without disclosing connection detail or borrowing the FND-02 business error envelope.
/// </summary>
[Collection(TestExecutionCollections.ConsoleSensitive)]
public sealed class HealthContractTests
{
    [Fact]
    public async Task LivenessSucceedsWithoutAnyConfiguredDatabase()
    {
        await using HealthApiFactory factory = new(connectionString: null);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.LivePath);

        await AssertLiveAsync(response);
    }

    [Fact]
    public async Task LivenessSucceedsWhilePostgreSqlIsUnreachable()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.LivePath);

        await AssertLiveAsync(response);
    }

    [Fact]
    public async Task LivenessNeverExecutesAReadinessTaggedCheck()
    {
        ReadinessProbeSpy spy = new();
        await using HealthApiFactory factory = new(
            HealthConnectionStrings.Unreachable,
            services => services.AddHealthChecks().AddCheck(
                "readiness-probe-spy",
                spy,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthContract.ReadinessTag]));
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage live = await client.GetAsync(HealthContract.LivePath))
        {
            await AssertLiveAsync(live);
        }

        Assert.Equal(0, spy.Invocations);

        using (HttpResponseMessage ready = await client.GetAsync(HealthContract.ReadyPath))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        }

        Assert.Equal(1, spy.Invocations);
    }

    [Fact]
    public async Task ReadinessFailsWhilePostgreSqlIsUnreachable()
    {
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);

        await AssertNotReadyAsync(response);
    }

    [Fact]
    public async Task ReadinessFailsAsAnOperationalFailureWithoutAConfiguredConnectionString()
    {
        await using HealthApiFactory factory = new(connectionString: null);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath);

        await AssertNotReadyAsync(response);
    }

    [Fact]
    public async Task ReadinessFailureDisclosesNoConnectionDetailInTheResponseOrTheLog()
    {
        using ConsoleCapture capture = new();

        await using (HealthApiFactory factory = new(HealthConnectionStrings.Unreachable))
        using (HttpClient client = factory.CreateClient())
        using (HttpResponseMessage response = await client.GetAsync(HealthContract.ReadyPath))
        {
            string body = await AssertNotReadyAsync(response);

            Assert.DoesNotContain(HealthConnectionStrings.Unreachable, body, StringComparison.Ordinal);

            foreach (string disclosure in HealthConnectionStrings.ProhibitedDisclosures)
            {
                Assert.DoesNotContain(disclosure, body, StringComparison.OrdinalIgnoreCase);
            }
        }

        string logs = capture.Content;

        // ADR-0008 keeps dependency failures in the technical log, so the allow-list there matches
        // FND-02: a fixed reason and an exception type, never connection detail or a stack trace.
        Assert.DoesNotContain(HealthConnectionStrings.Unreachable, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(HealthConnectionStrings.PasswordSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(HealthConnectionStrings.UsernameSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(HealthConnectionStrings.DatabaseSentinel, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "database_unreachable")]
    [InlineData(true, "dependency_failure")]
    public async Task ReadinessFailureReachesTheTechnicalLogAndNotTheResponse(
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

        // ADR-0008 keeps health-check anomalies in the technical log, not in the response.
        Assert.Contains(expectedReason, capture.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpointsKeepTheFnd02CorrelationIdContract()
    {
        const string supplied = "health-correlation_01.safe";
        await using HealthApiFactory factory = new(HealthConnectionStrings.Unreachable);
        using HttpClient client = factory.CreateClient();

        foreach (string path in new[] { HealthContract.LivePath, HealthContract.ReadyPath })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(
                supplied,
                Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        }
    }

    [Fact]
    public async Task UnknownHealthPathsStillUseTheBusinessEndpointNotFoundEnvelope()
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
        AssertPlainTextHealthResponse(response);
    }

    internal static async Task AssertReadyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthContract.HealthyBody, await response.Content.ReadAsStringAsync());
        AssertPlainTextHealthResponse(response);
    }

    internal static async Task<string> AssertNotReadyAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthContract.UnhealthyBody, body);
        AssertPlainTextHealthResponse(response);
        AssertNotABusinessErrorEnvelope(body);

        return body;
    }

    // AC-10: a health failure must never be rendered through the FND-02 business error contract.
    private static void AssertNotABusinessErrorEnvelope(string body)
    {
        Assert.DoesNotContain("\"code\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"message\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_failed", body, StringComparison.Ordinal);
        Assert.DoesNotContain("endpoint_not_found", body, StringComparison.Ordinal);
    }

    private static void AssertPlainTextHealthResponse(HttpResponseMessage response)
    {
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed class ReadinessProbeSpy : IHealthCheck
    {
        private int invocations;

        public int Invocations => Volatile.Read(ref invocations);

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}

internal static class HealthConnectionStrings
{
    public const string PasswordSentinel = "HEALTH_PASSWORD_SENTINEL_9F2C41";
    public const string UsernameSentinel = "health_username_sentinel_9f2c41";
    public const string DatabaseSentinel = "health_database_sentinel_9f2c41";

    /// <summary>A syntactically valid destination that no PostgreSQL server ever answers.</summary>
    public static string Unreachable { get; } = new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1",
        Port = 1,
        Database = DatabaseSentinel,
        Username = UsernameSentinel,
        Password = PasswordSentinel,
        Pooling = false,
        Timeout = 5,
        CommandTimeout = 5,
    }.ConnectionString;

    public static string[] ProhibitedDisclosures { get; } =
    [
        PasswordSentinel,
        UsernameSentinel,
        DatabaseSentinel,
        "Password=",
        "Host=",
        "127.0.0.1",
        "Npgsql",
        "Exception",
        "stack",
    ];
}

internal sealed class HealthApiFactory(
    string? connectionString,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        if (connectionString is not null)
        {
            builder.UseSetting(
                $"ConnectionStrings:{BankPersistence.ConnectionStringName}",
                connectionString);
        }

        if (configureServices is not null)
        {
            builder.ConfigureServices(configureServices);
        }
    }
}
