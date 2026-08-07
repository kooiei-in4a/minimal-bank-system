using System.Net;
using System.Net.Http.Json;
using MinimalBankSystem.Api.ErrorHandling;
using MinimalBankSystem.IntegrationTests.TestOnly;

namespace MinimalBankSystem.IntegrationTests.ErrorHandling;

public sealed class ErrorEnvelopeTests
{
    [Fact]
    public async Task UnmappedExceptionReturnsFixedSafeEnvelopeWith500Status()
    {
        await using ContractTestWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/__contract-test/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string rawBody = await response.Content.ReadAsStringAsync();
        ApiErrorResponse? body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.NotNull(body);
        Assert.Equal("internal_error", body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        Assert.DoesNotContain("Contract test deliberate unmapped exception", rawBody);
        Assert.DoesNotContain("InvalidOperationException", rawBody);
        Assert.DoesNotContain("   at ", rawBody);
        Assert.DoesNotContain("MinimalBankSystem", rawBody);
    }
}
