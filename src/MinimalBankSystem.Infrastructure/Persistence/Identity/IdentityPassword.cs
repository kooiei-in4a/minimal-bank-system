using Microsoft.AspNetCore.Identity;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence.Identity;

/// <summary>
/// ASP.NET Core Identity password hashing without IdentityRole, IdentityDbContext, or auth middleware.
/// </summary>
public static class IdentityPassword
{
    public static IPasswordHasher<Operator> CreateHasher() => new PasswordHasher<Operator>();

    public static OperatorPasswordHash Hash(string plaintextPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);

        Operator probe = Operator.Create(
            userName: "identity-password-probe",
            passwordHash: new OperatorPasswordHash("probe"),
            role: OperatorRole.Viewer,
            utcNow: DateTimeOffset.UnixEpoch,
            securityStamp: "probe");

        return new OperatorPasswordHash(CreateHasher().HashPassword(probe, plaintextPassword));
    }

    public static PasswordVerificationResult Verify(Operator operatorEntity, string plaintextPassword)
    {
        ArgumentNullException.ThrowIfNull(operatorEntity);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);

        return CreateHasher().VerifyHashedPassword(
            operatorEntity,
            operatorEntity.PasswordHash,
            plaintextPassword);
    }
}
