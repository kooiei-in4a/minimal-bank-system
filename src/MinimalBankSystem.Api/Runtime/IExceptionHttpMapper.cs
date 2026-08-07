namespace MinimalBankSystem.Api.Runtime;

public interface IExceptionHttpMapper
{
    bool TryMap(Exception exception, out ApiErrorDefinition mappedError);
}
