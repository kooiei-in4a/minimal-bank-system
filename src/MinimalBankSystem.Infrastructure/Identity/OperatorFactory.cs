using System.Globalization;

namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>
/// Assembles a physically valid <see cref="Operator"/> row: application-generated UUIDv7 id,
/// normalized login identifier, an ASP.NET Core Identity password hash and security stamp, and
/// UTC timestamps sourced from the injected <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// This factory is mechanical construction only. It owns none of the business/authorization
/// rules of Operator creation (uniqueness enforcement beyond the DB constraint, "who may create an
/// Operator", last-administrator protection, audit logging) — those remain OPR-CREATE-01
/// responsibility. WP2-ID-01 provides this seam only so the physical persistence contract has one
/// correct, reusable construction path instead of being reimplemented ad hoc by every caller
/// (including this leaf's own integration-test seed data).
/// </remarks>
public static class OperatorFactory
{
    public static Operator Create(
        TimeProvider timeProvider,
        string userName,
        string password,
        OperatorRole role,
        OperatorState state = OperatorState.Active,
        long authorizationStateVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        DateTimeOffset now = timeProvider.GetUtcNow();

        Operator created = new()
        {
            Id = Guid.CreateVersion7(now),
            UserName = userName,
            NormalizedUserName = Normalize(userName),
            PasswordHash = string.Empty,
            SecurityStamp = Guid.NewGuid().ToString(),
            Role = role,
            State = state,
            AuthorizationStateVersion = authorizationStateVersion,
            CreatedAt = now,
            UpdatedAt = now,
        };

        created.PasswordHash = OperatorPasswordHasher.HashPassword(created, password);

        return created;
    }

    private static string Normalize(string userName) =>
        userName.ToUpper(CultureInfo.InvariantCulture);
}
