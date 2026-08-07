using MinimalBankSystem.Domain;

namespace MinimalBankSystem.Api.ExceptionMapping;

public interface IExceptionMapper
{
    bool TryMap(Exception exception, out int statusCode, out ErrorResponse errorResponse);
}
