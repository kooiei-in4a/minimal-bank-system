namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Result of mapping an exception onto the common API error contract.
/// </summary>
/// <param name="StatusCode">HTTP status code selected according to specification section 16.2.</param>
/// <param name="Code">Fixed error code returned to the caller.</param>
/// <param name="Message">Human readable message that must not contain internal detail.</param>
public sealed record ApiError(int StatusCode, string Code, string Message)
{
    /// <summary>
    /// Projects this error onto the serialized error envelope.
    /// </summary>
    public ApiErrorResponse ToResponse() => new(Code, Message);
}
