using System.Net;
using System.Net.Http.Json;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Verifies the prohibited technical log field policy of ADR-0008 and AC-OPS-005.
/// </summary>
/// <remarks>
/// The sentinels below are obvious placeholders, not credential shaped values, so the repository
/// never contains anything resembling a real secret.
/// </remarks>
public sealed class ProhibitedLogFieldTests
{
    private const string PasswordSentinel = "prohibited-field-password-sentinel";
    private const string JwtSentinel = "prohibited-field-jwt-sentinel";
    private const string SigningKeySentinel = "prohibited-field-signing-key-sentinel";
    private const string IdempotencyKeySentinel = "prohibited-field-idempotency-key-sentinel";
    private const string ConnectionStringSentinel = "prohibited-field-connection-string-sentinel";
    private const string QueryStringSentinel = "prohibited-field-query-string-sentinel";

    private static readonly string[] Sentinels =
    [
        PasswordSentinel,
        JwtSentinel,
        SigningKeySentinel,
        IdempotencyKeySentinel,
        ConnectionStringSentinel,
        QueryStringSentinel,
    ];

    [Fact]
    public async Task CallerSuppliedSecretsReachNeitherTheTechnicalLogNorTheErrorResponse()
    {
        await using ApiRuntimeTestServer server = await ApiRuntimeTestServer.StartAsync();

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/{RuntimeContractTestController.RouteBase}/failure/unmapped?accessToken={QueryStringSentinel}")
        {
            Content = JsonContent.Create(new RuntimeContractTestPayload
            {
                Password = PasswordSentinel,
                SigningKey = SigningKeySentinel,
                ConnectionString = ConnectionStringSentinel,
            }),
        };

        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {JwtSentinel}");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", IdempotencyKeySentinel);

        string correlationId;
        using (HttpResponseMessage response = await server.Client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdPolicy.HeaderName));

            ApiErrorEnvelope envelope = await ApiErrorEnvelope.ReadAsync(response);
            Assert.Equal(ApiErrorCodes.InternalError, envelope.Code);

            foreach (string sentinel in Sentinels)
            {
                Assert.DoesNotContain(sentinel, envelope.RawBody, StringComparison.OrdinalIgnoreCase);
            }
        }

        string consoleLog = await server.StopAndReadConsoleLogAsync();

        // Guard: prove the failure really was logged, otherwise the assertions below pass vacuously.
        Assert.Contains(correlationId, consoleLog, StringComparison.Ordinal);
        Assert.Contains(ApiErrorCodes.InternalError, consoleLog, StringComparison.Ordinal);

        foreach (string sentinel in Sentinels)
        {
            Assert.DoesNotContain(sentinel, consoleLog, StringComparison.OrdinalIgnoreCase);
        }
    }
}
