namespace MinimalBankSystem.Api.Runtime;

public interface IApiExceptionMapper
{
    ApiExceptionMapping? TryMap(Exception exception);
}
