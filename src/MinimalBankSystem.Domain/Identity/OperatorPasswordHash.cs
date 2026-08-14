namespace MinimalBankSystem.Domain.Identity;

/// <summary>
/// Opaque password-hash value accepted by the Operator construction boundary.
/// The value can only be created by the owning password-hashing implementation or tests.
/// </summary>
public sealed class OperatorPasswordHash
{
    internal OperatorPasswordHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    internal string Value { get; }
}
