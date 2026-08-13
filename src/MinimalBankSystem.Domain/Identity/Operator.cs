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
        string passwordHash,
        OperatorRole role,
        DateTimeOffset utcNow,
        string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        string trimmedName = userName.Trim();
        if (trimmedName.Length > UserNameMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(userName));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        DateTimeOffset createdAt = utcNow.ToUniversalTime();

        return new Operator
        {
            Id = Guid.CreateVersion7(createdAt),
            UserName = trimmedName,
            NormalizedUserName = trimmedName.ToUpperInvariant(),
            PasswordHash = passwordHash,
            SecurityStamp = securityStamp,
            State = OperatorState.Active,
            Role = role,
            AuthorizationStateVersion = InitialAuthorizationStateVersion,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }
}
