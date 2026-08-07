namespace MinimalBankSystem.Api.ErrorHandling;

public sealed class DefaultExceptionMapper : IExceptionMapper
{
    private const int InternalServerError = 500;
    private const string InternalErrorCode = "internal_server_error";

    public (int StatusCode, string Code) Map(Exception exception)
    {
        if (exception is ProblemException problem)
        {
            return (problem.StatusCode, problem.Code);
        }

        return (InternalServerError, InternalErrorCode);
    }
}
