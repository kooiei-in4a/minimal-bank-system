using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MinimalBankSystem.Api.Models;
using MinimalBankSystem.IntegrationTests.Fixtures;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class ErrorContractTests : IClassFixture<TestApiServer>
{
    private readonly TestApiServer _server;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ErrorContractTests(TestApiServer server)
    {
        _server = server;
    }

    [Fact]
    public async Task UnmappedExceptionReturns500WithErrorEnvelope()
    {
        using HttpResponseMessage response = await _server.Client.GetAsync("/api/contract/error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ApiErrorResponse? error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InternalError, error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task UnmappedExceptionDoesNotExposeExceptionDetails()
    {
        using HttpResponseMessage response = await _server.Client.GetAsync("/api/contract/error");
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Test unmapped exception", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task UnmappedExceptionErrorEnvelopeHasRequiredFields()
    {
        using HttpResponseMessage response = await _server.Client.GetAsync("/api/contract/error");
        string body = await response.Content.ReadAsStringAsync();

        JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("code", out _));
        Assert.True(root.TryGetProperty("message", out _));
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    [Fact]
    public async Task ErrorEnvelopeDoesNotContainInternalFields()
    {
        using HttpResponseMessage response = await _server.Client.GetAsync("/api/contract/error");
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("stackTrace", body);
        Assert.DoesNotContain("innerException", body);
        Assert.DoesNotContain("details", body);
    }
}
