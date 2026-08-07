using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Logging;
using MinimalBankSystem.IntegrationTests.TestOnly;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class SensitiveLogFieldPolicyTests
{
    private const string PasswordSentinel = "sentinel-password-value-not-a-real-secret";
    private const string JwtSentinel = "sentinel-jwt-value-not-a-real-token";
    private const string SigningKeySentinel = "sentinel-signing-key-value-not-a-real-key";
    private const string IdempotencyKeySentinel = "sentinel-idempotency-key-value-not-real";
    private const string ConnectionStringSentinel = "sentinel-connection-string-value-not-real";
    private const string BenignFieldValue = "contract-test-benign-value";

    [Fact]
    public void SanitizeRedactsProhibitedFieldNamesPreservesOthers()
    {
        Dictionary<string, object?> fields = new()
        {
            ["Password"] = PasswordSentinel,
            ["Jwt"] = JwtSentinel,
            ["SigningKey"] = SigningKeySentinel,
            ["IdempotencyKey"] = IdempotencyKeySentinel,
            ["ConnectionString"] = ConnectionStringSentinel,
            ["OperationName"] = BenignFieldValue,
        };

        IReadOnlyDictionary<string, object?> sanitized = SensitiveLogFieldPolicy.Sanitize(fields);

        Assert.Equal(SensitiveLogFieldPolicy.RedactedValue, sanitized["Password"]);
        Assert.Equal(SensitiveLogFieldPolicy.RedactedValue, sanitized["Jwt"]);
        Assert.Equal(SensitiveLogFieldPolicy.RedactedValue, sanitized["SigningKey"]);
        Assert.Equal(SensitiveLogFieldPolicy.RedactedValue, sanitized["IdempotencyKey"]);
        Assert.Equal(SensitiveLogFieldPolicy.RedactedValue, sanitized["ConnectionString"]);
        Assert.Equal(BenignFieldValue, sanitized["OperationName"]);
    }

    [Fact]
    public async Task TechnicalLogDoesNotContainProhibitedSentinelValues()
    {
        string capturedOutput = await ConsoleCapture.CaptureAsync(async () =>
        {
            ServiceCollection services = new();
            services.AddLogging(builder => builder.AddTechnicalJsonConsoleLogging());
            await using ServiceProvider provider = services.BuildServiceProvider();

            ILogger<SensitiveLogFieldPolicyTests> logger =
                provider.GetRequiredService<ILogger<SensitiveLogFieldPolicyTests>>();

            IReadOnlyDictionary<string, object?> sanitizedFields = SensitiveLogFieldPolicy.Sanitize(
                new Dictionary<string, object?>
                {
                    ["Password"] = PasswordSentinel,
                    ["Jwt"] = JwtSentinel,
                    ["SigningKey"] = SigningKeySentinel,
                    ["IdempotencyKey"] = IdempotencyKeySentinel,
                    ["ConnectionString"] = ConnectionStringSentinel,
                    ["OperationName"] = BenignFieldValue,
                });

            using (logger.BeginScope(sanitizedFields))
            {
                SensitiveLogFieldPolicyTestsLog.ContractTestEntry(logger);
            }
        });

        Assert.DoesNotContain(PasswordSentinel, capturedOutput);
        Assert.DoesNotContain(JwtSentinel, capturedOutput);
        Assert.DoesNotContain(SigningKeySentinel, capturedOutput);
        Assert.DoesNotContain(IdempotencyKeySentinel, capturedOutput);
        Assert.DoesNotContain(ConnectionStringSentinel, capturedOutput);

        Assert.Contains(BenignFieldValue, capturedOutput);
    }
}

internal static partial class SensitiveLogFieldPolicyTestsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Sensitive field policy contract test entry.")]
    public static partial void ContractTestEntry(ILogger logger);
}
