namespace MinimalBankSystem.Api.Models;

public sealed record ApiErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}
