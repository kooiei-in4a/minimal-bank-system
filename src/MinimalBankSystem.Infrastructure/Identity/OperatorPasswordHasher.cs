using Microsoft.AspNetCore.Identity;

namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>
/// Thin wrapper over the real <see cref="PasswordHasher{TUser}"/> from ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Issue #165 requires "ASP.NET Core Identity hashing semantics", not a reimplementation of them.
/// This type adds no algorithm, iteration count or format of its own; it only fixes the <c>TUser</c>
/// type argument to <see cref="Operator"/> so every caller produces and verifies hashes using the
/// exact framework component a future AUTHN/OPR-CREATE leaf would also use.
/// </remarks>
public static class OperatorPasswordHasher
{
    private static readonly PasswordHasher<Operator> Hasher = new();

    /// <summary>Hashes <paramref name="password"/> for storage in <see cref="Operator.PasswordHash"/>.</summary>
    public static string HashPassword(Operator @operator, string password) =>
        Hasher.HashPassword(@operator, password);

    /// <summary>Verifies <paramref name="providedPassword"/> against a stored hash.</summary>
    public static PasswordVerificationResult VerifyHashedPassword(
        Operator @operator,
        string hashedPassword,
        string providedPassword) =>
        Hasher.VerifyHashedPassword(@operator, hashedPassword, providedPassword);
}
