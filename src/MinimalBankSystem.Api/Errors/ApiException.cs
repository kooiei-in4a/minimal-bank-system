namespace MinimalBankSystem.Api.Errors;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public string Code { get; }

    public ApiException(int statusCode, string code, string message)
        : base(message)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, StatusCodes.Status400BadRequest);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, 599);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        StatusCode = statusCode;
        Code = code;
    }
}
