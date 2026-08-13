using Microsoft.AspNetCore.Identity;

namespace MinimalBankSystem.Domain;

#pragma warning disable CA1716 // Operator is the product term fixed by the approved specification.

/// <summary>
/// Product Operator identity with the small Identity surface required by ADR-0007.
/// </summary>
/// <remarks>
/// The Identity base supplies the framework-owned identity fields, normalized login name,
/// password hash, security stamp and concurrency stamp. Product state, fixed role and
/// authorization-state version remain explicit product properties. The persistence mapping
/// deliberately ignores unused Identity features such as email, phone, lockout, two-factor,
/// claims, external logins, tokens and Identity roles.
/// </remarks>
public sealed class Operator : IdentityUser<Guid>
{
    /// <summary>Initial version used in newly created authorization state.</summary>
    public const long InitialAuthorizationStateVersion = 1;

    /// <summary>EF Core materialization constructor.</summary>
    private Operator()
    {
    }

    /// <summary>The product lifecycle state.</summary>
    public OperatorState State { get; private set; }

    /// <summary>The one current fixed product role.</summary>
    public OperatorRole Role { get; private set; }

    /// <summary>Version compared with authorization state carried by a token.</summary>
    public long AuthorizationStateVersion { get; private set; }

    /// <summary>UTC instant at which the Operator was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC instant at which the Operator was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates an Operator with an application-generated UUIDv7 and an ASP.NET Core Identity hash.
    /// </summary>
    /// <param name="userName">The individual login identifier.</param>
    /// <param name="password">The password, held only long enough for framework hashing.</param>
    /// <param name="role">The one fixed product role.</param>
    /// <param name="utcNow">The current UTC instant supplied by the application time abstraction.</param>
    /// <param name="passwordHasher">The ASP.NET Core Identity password hasher.</param>
    public static Operator Create(
        string userName,
        string password,
        OperatorRole role,
        DateTimeOffset utcNow,
        IPasswordHasher<Operator> passwordHasher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentOutOfRangeException.ThrowIfNotEqual(utcNow.Offset, TimeSpan.Zero);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The Operator role is not a fixed product role.");
        }

        string trimmedUserName = userName.Trim();
        Operator operatorIdentity = new()
        {
            Id = Guid.CreateVersion7(),
            UserName = trimmedUserName,
            NormalizedUserName = trimmedUserName.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            State = OperatorState.Active,
            Role = role,
            AuthorizationStateVersion = InitialAuthorizationStateVersion,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        operatorIdentity.PasswordHash = passwordHasher.HashPassword(operatorIdentity, password);
        return operatorIdentity;
    }
}

#pragma warning restore CA1716
