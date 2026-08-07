namespace MinimalBankSystem.Api.Runtime;

public sealed record ApiErrorResponse(string Code, string Message);

public sealed record ApiExceptionMapping(int StatusCode, ApiErrorResponse Error);
