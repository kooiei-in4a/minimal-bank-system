namespace MinimalBankSystem.Api.ErrorHandling;

public class ProblemException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public ProblemException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public ProblemException(int statusCode, string code, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
