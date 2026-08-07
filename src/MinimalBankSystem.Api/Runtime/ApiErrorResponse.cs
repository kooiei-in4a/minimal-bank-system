namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Common REST error envelope per specification §16.1.
/// </summary>
public sealed record ApiErrorResponse(string Code, string Message);
