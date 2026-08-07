using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies the technical logging baseline of ADR-0008: JSON console output that carries the
/// correlation identifier and the fixed error code.
/// </summary>
public sealed class TechnicalLoggingTests
{
    [Fact]
    public async Task UnmappedFailureIsLoggedAsJsonWithCorrelationIdAndFixedErrorCode()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        string correlationId;
        using (HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            $"/{RuntimeContractTestController.RouteBase}/failure/unmapped",
            new RuntimeContractTestPayload()))
        {
            correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdPolicy.HeaderName));
        }

        // ReadAll parses every console line as JSON, so a non JSON line fails the test.
        IReadOnlyList<TechnicalLogEntry> entries =
            TechnicalLogEntry.ReadAll(await server.StopAndReadConsoleLogAsync());

        TechnicalLogEntry failure = Assert.Single(
            entries,
            entry => entry.GetStateString("ErrorCode") == ApiErrorCodes.InternalError);

        Assert.Equal("Error", failure.LogLevel);
        Assert.Equal("500", failure.GetStateString("StatusCode"));
        Assert.True(failure.HasScopeValue("CorrelationId", correlationId));
    }

    [Fact]
    public async Task MappedFailureIsLoggedWithItsFixedErrorCode()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync(
            services => services.AddApiExceptionMapper<RuntimeContractTestExceptionMapper>());

        string correlationId;
        using (HttpResponseMessage response = await server.Client.GetAsync(
            $"/{RuntimeContractTestController.RouteBase}/failure/mapped"))
        {
            correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdPolicy.HeaderName));
        }

        IReadOnlyList<TechnicalLogEntry> entries =
            TechnicalLogEntry.ReadAll(await server.StopAndReadConsoleLogAsync());

        TechnicalLogEntry failure = Assert.Single(
            entries,
            entry => entry.GetStateString("ErrorCode") == RuntimeContractTestExceptionMapper.ErrorCode);

        Assert.Equal("Warning", failure.LogLevel);
        Assert.True(failure.HasScopeValue("CorrelationId", correlationId));
    }

    [Fact]
    public async Task OrdinaryApplicationLogsCarryTheCorrelationScope()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        string correlationId;
        using (HttpResponseMessage response = await server.Client.GetAsync(
            $"/{RuntimeContractTestController.RouteBase}/correlation"))
        {
            correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdPolicy.HeaderName));
        }

        IReadOnlyList<TechnicalLogEntry> entries =
            TechnicalLogEntry.ReadAll(await server.StopAndReadConsoleLogAsync());

        Assert.Contains(
            entries,
            entry => entry.Category == typeof(RuntimeContractTestController).FullName
                && entry.HasScopeValue("CorrelationId", correlationId));
    }
}
