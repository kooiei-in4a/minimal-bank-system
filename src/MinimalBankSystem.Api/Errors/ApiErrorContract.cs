namespace MinimalBankSystem.Api.Errors;

public sealed record ApiErrorEnvelope(string Code, string Message);

public sealed record ApiErrorMapping(int StatusCode, string Code, string Message);

public interface IExceptionToHttpMapper
{
    ApiErrorMapping? Map(Exception exception);
}
