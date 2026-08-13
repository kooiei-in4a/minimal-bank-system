namespace MinimalBankSystem.Domain.Identity;

/// <summary>Persisted Operator lifecycle state. Stored as lowercase text.</summary>
public enum OperatorState
{
    Active = 0,
    Disabled = 1,
}
