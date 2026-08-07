using System.Net;
using System.Text.Json;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class ErrorContractTests : IClassFixture<ApiContractWebApplicationFactory>
{
    private readonly ApiContractWebApplicationFactory _factory;

    public ErrorContractTests(ApiContractWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnmappedException_ReturnsSpecificationErrorEnvelopeWithoutInternalDetails()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/__contract__/unmapped-exception");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal(ApiErrorCatalog.UnmappedException.Code, root.GetProperty("code").GetString());
        Assert.Equal(ApiErrorCatalog.UnmappedException.Message, root.GetProperty("message").GetString());
        Assert.False(body.Contains("contract-unmapped-sentinel-detail", StringComparison.Ordinal));
        Assert.False(body.Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.False(body.Contains("stack", StringComparison.OrdinalIgnoreCase));
        Assert.False(body.Contains("trace", StringComparison.OrdinalIgnoreCase));
    }
}
