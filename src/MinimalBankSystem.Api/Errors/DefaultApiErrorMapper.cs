namespace MinimalBankSystem.Api.Errors;

public sealed class DefaultApiErrorMapper : IApiErrorMapper
{
    public ApiErrorResult Map(Exception exception)
    {
        if (exception is ApiException apiException)
        {
            return new ApiErrorResult(apiException.StatusCode, new ApiErrorEnvelope(apiException.Code, apiException.Message));
        }

        return new ApiErrorResult(StatusCodes.Status500InternalServerError, ApiErrorEnvelope.InternalError);
    }
}
