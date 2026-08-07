using MinimalBankSystem.Api.Correlation;
using MinimalBankSystem.IntegrationTests.TestOnly;

namespace MinimalBankSystem.IntegrationTests.Correlation;

public sealed class CorrelationIdTests
{
    [Fact]
    public async Task NoHeaderSuppliedGeneratesCorrelationIdInResponseHeader()
    {
        await using ContractTestWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/__contract-test/echo");

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? values));
        string generated = Assert.Single(values!);
        Assert.False(string.IsNullOrWhiteSpace(generated));
    }

    [Fact]
    public async Task ValidCallerSuppliedCorrelationIdIsEchoedBack()
    {
        await using ContractTestWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        const string suppliedId = "caller-supplied-id-123";
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract-test/echo");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);

        HttpResponseMessage response = await client.SendAsync(request);

        string echoed = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.Equal(suppliedId, echoed);
    }

    [Fact]
    public async Task InvalidCallerSuppliedCorrelationIdIsNotTrusted()
    {
        await using ContractTestWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        const string maliciousId = "bad id; \n<script>DROP TABLE</script>";
        using HttpRequestMessage request = new(HttpMethod.Get, "/__contract-test/echo");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, maliciousId);

        HttpResponseMessage response = await client.SendAsync(request);

        string resolved = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.NotEqual(maliciousId, resolved);
        Assert.Matches("^[A-Za-z0-9_-]{1,128}$", resolved);
    }

    [Fact]
    public async Task CorrelationIdAppearsInTechnicalLogForFailedRequest()
    {
        const string suppliedId = "contract-test-correlation-abc";
        string responseCorrelationId = string.Empty;

        string capturedLog = await ConsoleCapture.CaptureAsync(async () =>
        {
            await using ContractTestWebApplicationFactory factory = new();
            using HttpClient client = factory.CreateClient();

            using HttpRequestMessage request = new(HttpMethod.Get, "/__contract-test/throw");
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);

            HttpResponseMessage response = await client.SendAsync(request);
            responseCorrelationId = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        });

        Assert.Equal(suppliedId, responseCorrelationId);
        Assert.Contains(suppliedId, capturedLog);
    }
}
