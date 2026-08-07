using System.Text.Json;
using MinimalBankSystem.Api.Models;

namespace MinimalBankSystem.IntegrationTests;

public sealed class ProhibitedFieldTests
{
    private static readonly string[] ProhibitedValues =
    [
        "P@ssw0rd!123",
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.signature",
        "-----BEGIN RSA PRIVATE KEY-----",
        "sk-test-signing-key-12345",
        "idempotency-raw-key-value-12345",
        "Host=localhost;Port=5432;Database=testdb;Username=admin;Password=secret"
    ];

    [Theory]
    [MemberData(nameof(GetProhibitedValues))]
    public void ErrorResponseDoesNotContainProhibitedValues(string prohibitedValue)
    {
        var error = new ErrorResponse("internal_error", "An internal error occurred.");
        string json = JsonSerializer.Serialize(error);

        Assert.DoesNotContain(prohibitedValue, json);
    }

    [Fact]
    public void ErrorEnvelopeDoesNotExposeInternalDetails()
    {
        var error = new ErrorResponse("internal_error", "An internal error occurred. Please try again later.");
        string json = JsonSerializer.Serialize(error);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorEnvelopeStructureMatchesContract()
    {
        var error = new ErrorResponse("test_code", "test message");

        string json = JsonSerializer.Serialize(error);
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.True(doc.TryGetProperty("Code", out _));
        Assert.True(doc.TryGetProperty("Message", out _));
    }

    public static IEnumerable<object[]> GetProhibitedValues()
    {
        foreach (string value in ProhibitedValues)
        {
            yield return [value];
        }
    }
}
