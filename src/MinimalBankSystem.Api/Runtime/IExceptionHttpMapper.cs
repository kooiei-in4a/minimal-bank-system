namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Extension point for mapping known exceptions to HTTP status and the common error envelope.
/// FND-02 registers no business mappers; later Issues may add implementations.
/// </summary>
public interface IExceptionHttpMapper
{
    bool TryMap(Exception exception, out int statusCode, out ApiErrorResponse errorResponse);
}
