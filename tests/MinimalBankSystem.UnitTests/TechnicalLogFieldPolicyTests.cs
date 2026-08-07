using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.UnitTests;

public sealed class TechnicalLogFieldPolicyTests
{
    [Fact]
    public void SanitizeState_RemovesProhibitedKeys()
    {
        KeyValuePair<string, object?>[] state =
        [
            new("password", "sentinel-password-value"),
            new("probe", "allowed"),
            new("connection_string", "sentinel-connection-string-value"),
        ];

        IReadOnlyList<KeyValuePair<string, object?>> sanitized = TechnicalLogFieldPolicy.SanitizeState(state);

        Assert.Single(sanitized);
        Assert.Equal("probe", sanitized[0].Key);
        Assert.Equal("allowed", sanitized[0].Value);
    }
}
