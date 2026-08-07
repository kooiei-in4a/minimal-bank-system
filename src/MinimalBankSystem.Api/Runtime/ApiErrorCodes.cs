namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Fixed error codes owned by the common API runtime contract.
/// </summary>
/// <remarks>
/// Business specific codes of specification section 16.3 belong to the feature that raises them and
/// are attached through <see cref="IApiExceptionMapper"/>. They are intentionally not declared here.
/// </remarks>
public static class ApiErrorCodes
{
    /// <summary>
    /// Request input failed the framework level input validation contract (HTTP 400).
    /// </summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>
    /// An exception reached the API boundary without a mapping (HTTP 500).
    /// The response carries this code only and never discloses the internal failure.
    /// </summary>
    public const string InternalError = "internal_error";
}
