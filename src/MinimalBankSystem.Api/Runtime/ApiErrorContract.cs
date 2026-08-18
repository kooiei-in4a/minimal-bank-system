namespace MinimalBankSystem.Api.Runtime;

public sealed record ApiErrorEnvelope(string Code, string Message)
{
    public static ApiErrorEnvelope ValidationFailed { get; } =
        new("validation_failed", "The request is invalid.");

    public static ApiErrorEnvelope AuthenticationRequired { get; } =
        new("authentication_required", "Authentication is required.");

    public static ApiErrorEnvelope OperationNotPermitted { get; } =
        new("operation_not_permitted", "The authenticated operator is not permitted to perform this operation.");

    public static ApiErrorEnvelope OperatorNotFound { get; } =
        new("operator_not_found", "The requested operator was not found.");

    public static ApiErrorEnvelope OperatorLoginIdentifierAlreadyRegistered { get; } =
        new(
            "operator_login_identifier_already_registered",
            "The operator login identifier is already registered.");

    public static ApiErrorEnvelope StateTransitionNotAllowed { get; } =
        new("state_transition_not_allowed", "The requested operator state transition is not allowed.");

    public static ApiErrorEnvelope ConcurrentOperationConflict { get; } =
        new("concurrent_operation_conflict", "The operator mutation conflicted with a concurrent operation.");
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
