using MinimalBankSystem.Domain;

namespace MinimalBankSystem.Api.ExceptionMapping;

public sealed class DefaultExceptionMapper : IExceptionMapper
{
    public bool TryMap(Exception exception, out int statusCode, out ErrorResponse errorResponse)
    {
        if (exception is ApiException apiException)
        {
            statusCode = apiException.StatusCode;
            errorResponse = new ErrorResponse(apiException.ErrorCode, apiException.Message);
            return true;
        }

        statusCode = 0;
        errorResponse = default!;
        return false;
    }
}
