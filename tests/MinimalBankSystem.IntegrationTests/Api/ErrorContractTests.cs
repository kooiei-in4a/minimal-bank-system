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
    public async Task UnmappedExceptionReturnsSafeInternalErrorEnvelope()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/__contract__/unmapped-exception");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(ApiErrorCatalog.InternalErrorCode, root.GetProperty("code").GetString());
        Assert.Equal(ApiErrorCatalog.InternalErrorMessage, root.GetProperty("message").GetString());

        Assert.DoesNotContain("PROBE_UNMAPPED_DETAIL", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_PASSWORD_VALUE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_JWT_VALUE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
    }
}
