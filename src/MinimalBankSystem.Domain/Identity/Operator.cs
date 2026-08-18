using System.Diagnostics.CodeAnalysis;

namespace MinimalBankSystem.Domain.Identity;

/// <summary>Identity / Operator persistence entity owned by WP2-ID-01.</summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Operator is the approved product term in the specification and ADR-0007.")]
public sealed class Operator
{
    public const int UserNameMaxLength = 256;
    public const int InitialAuthorizationStateVersion = 1;

    private Operator()
    {
        UserName = null!;
        NormalizedUserName = null!;
        PasswordHash = null!;
        SecurityStamp = null!;
    }

    public Guid Id { get; private set; }

    public string UserName { get; private set; }

    public string NormalizedUserName { get; private set; }

    public string PasswordHash { get; private set; }

    public string SecurityStamp { get; private set; }

    public OperatorState State { get; private set; }

    public OperatorRole Role { get; private set; }

    public int AuthorizationStateVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Operator Create(
        string userName,
        OperatorPasswordHash passwordHash,
        OperatorRole role,
        DateTimeOffset utcNow,
        string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        string trimmedName = userName.Trim();
        if (trimmedName.Length > UserNameMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(userName));
        }

        if (role is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        DateTimeOffset createdAt = utcNow.ToUniversalTime();

        return new Operator
        {
            Id = Guid.CreateVersion7(createdAt),
            UserName = trimmedName,
            NormalizedUserName = trimmedName.ToUpperInvariant(),
            PasswordHash = passwordHash.Value,
            SecurityStamp = securityStamp,
            State = OperatorState.Active,
            Role = role,
            AuthorizationStateVersion = InitialAuthorizationStateVersion,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    /// <summary>
    /// Transitions this Operator to <see cref="OperatorState.Active"/> as an actual state change.
    /// Callers own no-op detection (already-active), self-disable and last-administrator
    /// invariants; this method unconditionally applies the transition and the resulting
    /// authorization-state invalidation required by ADR-0007.
    /// </summary>
    public void Enable(DateTimeOffset utcNow, string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        State = OperatorState.Active;
        InvalidateAuthorizationState(utcNow, securityStamp);
    }

    /// <summary>
    /// Transitions this Operator to <see cref="OperatorState.Disabled"/> as an actual state
    /// change. Callers own no-op detection (already-disabled), self-disable and
    /// last-administrator invariants; this method unconditionally applies the transition.
    /// </summary>
    public void Disable(DateTimeOffset utcNow, string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        State = OperatorState.Disabled;
        InvalidateAuthorizationState(utcNow, securityStamp);
    }

    /// <summary>
    /// Changes this Operator's fixed role as an actual role change. Callers own no-op detection
    /// (same-role) and last-administrator invariants; this method unconditionally applies the
    /// transition.
    /// </summary>
    public void ChangeRole(OperatorRole newRole, DateTimeOffset utcNow, string securityStamp)
    {
        if (newRole is not (OperatorRole.Administrator or OperatorRole.Teller or OperatorRole.Viewer))
        {
            throw new ArgumentOutOfRangeException(nameof(newRole));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        Role = newRole;
        InvalidateAuthorizationState(utcNow, securityStamp);
    }

    private void InvalidateAuthorizationState(DateTimeOffset utcNow, string securityStamp)
    {
        checked
        {
            AuthorizationStateVersion += 1;
        }

        SecurityStamp = securityStamp;
        UpdatedAt = utcNow.ToUniversalTime();
    }
}
