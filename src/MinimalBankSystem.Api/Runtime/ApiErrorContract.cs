namespace MinimalBankSystem.Api.Runtime;

public sealed record ApiErrorEnvelope(string Code, string Message)
{
    public static ApiErrorEnvelope ValidationFailed { get; } =
        new("validation_failed", "The request is invalid.");

    public static ApiErrorEnvelope AuthenticationRequired { get; } =
        new("authentication_required", "Authentication is required.");
}

public sealed record ApiErrorMapping(int StatusCode, string Code, string Message)
{
    internal static ApiErrorMapping InternalError { get; } =
        new(StatusCodes.Status500InternalServerError, "internal_error", "An internal error occurred.");

    internal static ApiErrorMapping? FromFrameworkStatusCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status404NotFound => new(
                StatusCodes.Status404NotFound,
                "endpoint_not_found",
                "The requested endpoint was not found."),
            StatusCodes.Status405MethodNotAllowed => new(
                StatusCodes.Status405MethodNotAllowed,
                "method_not_allowed",
                "The HTTP method is not allowed for this endpoint."),
            StatusCodes.Status415UnsupportedMediaType => new(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported_media_type",
                "The request media type is not supported."),
            _ => null,
        };
}

public interface IApiExceptionMapper
{
    ApiErrorMapping? TryMap(Exception exception);
}
