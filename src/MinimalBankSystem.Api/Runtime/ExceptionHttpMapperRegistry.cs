namespace MinimalBankSystem.Api.Runtime;

public sealed class ExceptionHttpMapperRegistry
{
    private readonly IReadOnlyList<IExceptionHttpMapper> _mappers;

    public ExceptionHttpMapperRegistry(IEnumerable<IExceptionHttpMapper> mappers)
    {
        _mappers = mappers.ToList();
    }

    public bool TryMap(Exception exception, out ApiErrorDefinition mappedError)
    {
        foreach (IExceptionHttpMapper mapper in _mappers)
        {
            if (mapper.TryMap(exception, out mappedError))
            {
                return true;
            }
        }

        mappedError = ApiErrorCatalog.UnmappedException;
        return false;
    }
}
