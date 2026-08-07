namespace MinimalBankSystem.Api.RuntimeContract;

public sealed record ApiErrorResponse(string Code, string Message);

public sealed record ApiErrorMapping(int StatusCode, string Code, string Message)
{
    public static ApiErrorMapping InternalError { get; } =
        new(StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.");
}

public interface IApiExceptionMapper
{
    ApiErrorMapping? Map(Exception exception);
}

public sealed class DefaultApiExceptionMapper : IApiExceptionMapper
{
    public ApiErrorMapping? Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return null;
    }
}
