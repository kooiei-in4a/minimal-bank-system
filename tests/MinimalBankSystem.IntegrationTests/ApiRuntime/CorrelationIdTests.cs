using System.Net;
using System.Text.Json;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies that one correlation identifier is established per request, is reachable from the
/// response, and that caller supplied values are never trusted as they arrive.
/// </summary>
public sealed class CorrelationIdTests
{
    private static string CorrelationRoute => $"/{RuntimeContractTestController.RouteBase}/correlation";

    [Fact]
    public async Task GeneratesACorrelationIdWhenTheCallerSuppliesNone()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync(CorrelationRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string responseCorrelationId = ReadCorrelationHeader(response);
        Assert.True(CorrelationIdPolicy.IsAcceptable(responseCorrelationId));
        Assert.Equal(responseCorrelationId, await ReadCorrelationBodyAsync(response));
    }

    [Fact]
    public async Task ReusesAnAcceptableCallerSuppliedCorrelationId()
    {
        const string callerCorrelationId = "caller-supplied-0123456789";

        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage response = await SendAsync(server, callerCorrelationId);

        Assert.Equal(callerCorrelationId, ReadCorrelationHeader(response));
        Assert.Equal(callerCorrelationId, await ReadCorrelationBodyAsync(response));
    }

    [Theory]
    [InlineData("injected\r\nX-Injected: attacker-controlled")]
    [InlineData("injected\nsecond-line")]
    [InlineData("control\u0000character")]
    [InlineData("correlation id with spaces")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("../../etc/passwd")]
    public async Task ReplacesAnUnsafeCallerSuppliedCorrelationId(string unsafeCorrelationId)
    {
        await AssertReplacedAsync(unsafeCorrelationId);
    }

    [Fact]
    public async Task ReplacesAnOverlongCallerSuppliedCorrelationId()
    {
        await AssertReplacedAsync(new string('a', CorrelationIdPolicy.MaxLength + 1));
    }

    [Fact]
    public async Task ReplacesRepeatedCallerSuppliedCorrelationIdHeaders()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, CorrelationRoute);
        request.Headers.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, "first-value");
        request.Headers.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, "second-value");

        using HttpResponseMessage response = await server.Client.SendAsync(request);

        string effectiveCorrelationId = ReadCorrelationHeader(response);
        Assert.NotEqual("first-value", effectiveCorrelationId);
        Assert.NotEqual("second-value", effectiveCorrelationId);
        Assert.True(CorrelationIdPolicy.IsAcceptable(effectiveCorrelationId));
    }

    [Fact]
    public async Task EachRequestGetsItsOwnCorrelationId()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpResponseMessage first = await server.Client.GetAsync(CorrelationRoute);
        using HttpResponseMessage second = await server.Client.GetAsync(CorrelationRoute);

        Assert.NotEqual(ReadCorrelationHeader(first), ReadCorrelationHeader(second));
    }

    private static async Task AssertReplacedAsync(string unsafeCorrelationId)
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        string effectiveCorrelationId;
        using (HttpResponseMessage response = await SendAsync(server, unsafeCorrelationId))
        {
            effectiveCorrelationId = ReadCorrelationHeader(response);

            // The application must have observed the untrusted value, otherwise the rejection below
            // would only prove that the transport dropped it.
            Assert.Contains(unsafeCorrelationId, await ReadSuppliedCorrelationIdsAsync(response));

            Assert.NotEqual(unsafeCorrelationId, effectiveCorrelationId);
            Assert.True(CorrelationIdPolicy.IsAcceptable(effectiveCorrelationId));
            Assert.Equal(effectiveCorrelationId, await ReadCorrelationBodyAsync(response));
        }

        string consoleLog = await server.StopAndReadConsoleLogAsync();
        Assert.Contains(effectiveCorrelationId, consoleLog, StringComparison.Ordinal);
        Assert.DoesNotContain(unsafeCorrelationId, consoleLog, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendAsync(ApiRuntimeTestServer server, string correlationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, CorrelationRoute);
        request.Headers.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, correlationId);

        return await server.Client.SendAsync(request);
    }

    private static string ReadCorrelationHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(CorrelationIdPolicy.HeaderName, out IEnumerable<string>? values));

        return Assert.Single(values!);
    }

    private static async Task<string> ReadCorrelationBodyAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("correlationId").GetString() ?? string.Empty;
    }

    private static async Task<string[]> ReadSuppliedCorrelationIdsAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return [.. document.RootElement
            .GetProperty("suppliedCorrelationIds")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)];
    }
}
