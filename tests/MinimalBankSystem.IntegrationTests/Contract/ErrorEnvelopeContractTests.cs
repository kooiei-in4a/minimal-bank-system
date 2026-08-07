using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.IntegrationTests.Infrastructure;
using Xunit;

namespace MinimalBankSystem.IntegrationTests.Contract;

public sealed class ErrorEnvelopeContractTests
{
    [Fact]
    public async Task UnmappedExceptionReturnsFixedEnvelopeAndHttp500()
    {
        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/_contract/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        Assert.Equal("internal_error", root.GetProperty("code").GetString());
        string message = root.GetProperty("message").GetString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.DoesNotContain("secret-detail", message);
        Assert.DoesNotContain("InvalidOperationException", message);

        Assert.Equal(2, root.EnumerateObject().Count());
    }

    [Fact]
    public async Task ErrorEnvelopeNeverIncludesStackOrType()
    {
        using IHost host = TestHostFactory.Build();
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/_contract/boom");
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", body, StringComparison.Ordinal);
    }
}
