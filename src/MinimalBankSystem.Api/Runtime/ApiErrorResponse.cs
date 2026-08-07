using System.Text.Json.Serialization;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Common REST error envelope of specification section 16.1 and AC-ERR-001.
/// </summary>
/// <remarks>
/// The envelope carries a fixed machine readable code and a human readable message only.
/// Internal exception detail, stack traces, SQL, credentials, secrets, tokens and unnecessary
/// personal data are never placed in this contract. The property names are pinned by attribute so
/// that the wire contract cannot drift with serializer configuration.
/// </remarks>
/// <param name="Code">Fixed error code. Callers use this value for machine decisions.</param>
/// <param name="Message">Human readable explanation. Callers must not match on this text.</param>
public sealed record ApiErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
