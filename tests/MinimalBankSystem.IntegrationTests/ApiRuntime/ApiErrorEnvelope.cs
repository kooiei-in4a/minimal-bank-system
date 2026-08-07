using System.Text.Json;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// The common REST error envelope as observed on the wire.
/// </summary>
internal sealed record ApiErrorEnvelope(string Code, string Message, string RawBody)
{
    private static readonly string[] ContractProperties = ["code", "message"];

    /// <summary>
    /// Reads the response body and asserts that it carries the specification's error envelope and
    /// nothing else.
    /// </summary>
    public static async Task<ApiErrorEnvelope> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(ContractProperties, root.EnumerateObject().Select(property => property.Name).ToArray());

        string code = root.GetProperty("code").GetString() ?? string.Empty;
        string message = root.GetProperty("message").GetString() ?? string.Empty;

        Assert.NotEmpty(code);
        Assert.NotEmpty(message);

        return new ApiErrorEnvelope(code, message, body);
    }
}
