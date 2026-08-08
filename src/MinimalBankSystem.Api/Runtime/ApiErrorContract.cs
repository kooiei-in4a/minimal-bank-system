namespace MinimalBankSystem.Api.Runtime;

public sealed record ApiErrorEnvelope(string Code, string Message)
{
    public static ApiErrorEnvelope ValidationFailed { get; } =
        new("validation_failed", "The request is invalid.");
}

public sealed record ApiErrorMapping(int StatusCode, string Code, string Message)
{
    internal static ApiErrorMapping InternalError { get; } =
        new(StatusCodes.Status500InternalServerError, "internal_error", "An internal error occurred.");
}

public interface IApiExceptionMapper
{
    ApiErrorMapping? TryMap(Exception exception);
}
