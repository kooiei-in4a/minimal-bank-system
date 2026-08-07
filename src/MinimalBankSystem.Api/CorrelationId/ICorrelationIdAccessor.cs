namespace MinimalBankSystem.Api.CorrelationId;

public interface ICorrelationIdAccessor
{
    string? Current { get; }
}
