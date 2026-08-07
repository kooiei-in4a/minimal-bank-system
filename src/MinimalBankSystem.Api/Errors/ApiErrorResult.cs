namespace MinimalBankSystem.Api.Errors;

public sealed record ApiErrorResult(int StatusCode, ApiErrorEnvelope Envelope);
