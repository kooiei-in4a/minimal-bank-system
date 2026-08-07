namespace MinimalBankSystem.Api.CorrelationId;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Store = new();

    public string? Current
    {
        get => Store.Value;
        internal set => Store.Value = value;
    }
}
