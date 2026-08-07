using System.Text.Json.Serialization;

namespace MinimalBankSystem.Api.ErrorHandling;

public sealed record ApiErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
