namespace MinimalBankSystem.Api.ErrorHandling;

public interface IExceptionMapper
{
    (int StatusCode, string Code) Map(Exception exception);
}
