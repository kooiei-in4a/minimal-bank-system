namespace MinimalBankSystem.Api.ErrorHandling;

public sealed record ApiError(int HttpStatusCode, string Code, string Message);
