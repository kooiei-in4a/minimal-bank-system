namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Tries registered <see cref="IExceptionHttpMapper"/> implementations in registration order.
/// </summary>
public sealed class ExceptionHttpMapperRegistry
{
    private readonly IEnumerable<IExceptionHttpMapper> _mappers;

    public ExceptionHttpMapperRegistry(IEnumerable<IExceptionHttpMapper> mappers)
    {
        _mappers = mappers;
    }

    public bool TryMap(Exception exception, out int statusCode, out ApiErrorResponse errorResponse)
    {
        foreach (IExceptionHttpMapper mapper in _mappers)
        {
            if (mapper.TryMap(exception, out statusCode, out errorResponse))
            {
                return true;
            }
        }

        statusCode = StatusCodes.Status500InternalServerError;
        errorResponse = new ApiErrorResponse(ApiErrorCatalog.InternalErrorCode, ApiErrorCatalog.InternalErrorMessage);
        return false;
    }
}
