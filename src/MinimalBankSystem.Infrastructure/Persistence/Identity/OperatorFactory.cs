using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence.Identity;

/// <summary>
/// Supported production construction path for persisted Operators.
/// It always hashes the supplied plaintext with ASP.NET Core Identity semantics.
/// </summary>
public static class OperatorFactory
{
    public static Operator Create(
        string userName,
        string plaintextPassword,
        OperatorRole role,
        DateTimeOffset utcNow,
        string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);

        return Operator.Create(
            userName,
            IdentityPassword.Hash(plaintextPassword),
            role,
            utcNow,
            securityStamp);
    }
}
