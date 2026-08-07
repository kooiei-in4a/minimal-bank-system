namespace MinimalBankSystem.Api.Errors;

public sealed record ErrorMapping(int StatusCode, string Code, string Message);

public interface IExceptionToHttpMapper
{
    bool TryMap(Exception exception, out ErrorMapping mapping);
}

public sealed class DefaultExceptionToHttpMapper : IExceptionToHttpMapper
{
    public bool TryMap(Exception exception, out ErrorMapping mapping)
    {
        mapping = new(
            StatusCodes.Status500InternalServerError,
            "data_integrity_violation",
            "An internal error occurred.");

        return true;
    }
}
