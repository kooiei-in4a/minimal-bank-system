using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.IntegrationTests.Infrastructure;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class CorrelationAndLoggingTests : IClassFixture<ApiContractWebApplicationFactory>
{
    private readonly ApiContractWebApplicationFactory _factory;

    public CorrelationAndLoggingTests(ApiContractWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ResponseIncludesGeneratedCorrelationIdWhenCallerOmitsHeader()
    {
        _factory.LoggerProvider.Clear();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/__contract__/time");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string correlationId = response.Headers.GetValues(CorrelationId.HeaderName).Single();
        Assert.True(CorrelationId.IsValidCallerSupplied(correlationId));

        CollectedLogEntry logEntry = _factory.LoggerProvider.Entries
            .Last(entry => entry.Properties.ContainsKey(CorrelationId.LogPropertyName));

        Assert.Equal(correlationId, logEntry.Properties[CorrelationId.LogPropertyName]?.ToString());
    }

    [Fact]
    public async Task ValidCallerSuppliedCorrelationId_IsPropagatedToResponseAndLogs()
    {
        _factory.LoggerProvider.Clear();
        const string callerCorrelationId = "caller-trace-001";

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract__/time");
        request.Headers.Add(CorrelationId.HeaderName, callerCorrelationId);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(callerCorrelationId, response.Headers.GetValues(CorrelationId.HeaderName).Single());

        CollectedLogEntry logEntry = _factory.LoggerProvider.Entries
            .Last(entry => entry.Properties.ContainsKey(CorrelationId.LogPropertyName));

        Assert.Equal(callerCorrelationId, logEntry.Properties[CorrelationId.LogPropertyName]?.ToString());
    }

    [Fact]
    public async Task InvalidCallerSuppliedCorrelationId_IsNotTrusted()
    {
        _factory.LoggerProvider.Clear();
        const string dangerousCorrelationId = "invalid/../correlation";

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract__/time");
        request.Headers.Add(CorrelationId.HeaderName, dangerousCorrelationId);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string correlationId = response.Headers.GetValues(CorrelationId.HeaderName).Single();
        Assert.NotEqual(dangerousCorrelationId, correlationId);
        Assert.True(CorrelationId.IsValidCallerSupplied(correlationId));
    }

    [Fact]
    public async Task TechnicalLogs_ContainCorrelationIdAndFixedErrorCode()
    {
        _factory.LoggerProvider.Clear();

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/__contract__/unmapped-exception");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string correlationId = response.Headers.GetValues(CorrelationId.HeaderName).Single();

        CollectedLogEntry errorLog = _factory.LoggerProvider.Entries
            .Last(entry => entry.Level == LogLevel.Error);

        Assert.Equal(correlationId, errorLog.Properties[CorrelationId.LogPropertyName]?.ToString());
        Assert.Equal(ApiErrorCatalog.UnmappedException.Code, errorLog.Properties["ErrorCode"]?.ToString());

        string json = JsonSerializer.Serialize(errorLog.Properties);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(correlationId, document.RootElement.GetProperty(CorrelationId.LogPropertyName).GetString());
        Assert.Equal(
            ApiErrorCatalog.UnmappedException.Code,
            document.RootElement.GetProperty("ErrorCode").GetString());
    }
}
