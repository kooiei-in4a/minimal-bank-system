namespace MinimalBankSystem.Api.Errors;

public interface IApiErrorMapper
{
    ApiErrorResult Map(Exception exception);
}
