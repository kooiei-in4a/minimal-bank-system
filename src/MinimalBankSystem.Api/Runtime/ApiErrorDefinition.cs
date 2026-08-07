namespace MinimalBankSystem.Api.Runtime;

public sealed record ApiErrorDefinition(string Code, string Message, int StatusCode);
