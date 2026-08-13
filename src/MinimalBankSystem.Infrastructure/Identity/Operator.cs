using System.Diagnostics.CodeAnalysis;

namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>
/// Persisted Operator identity shape (specification §4.4, ADR-0006, ADR-0007).
/// </summary>
/// <remarks>
/// WP2-ID-01 owns persistence/schema representation and round-trip availability of this shape
/// only. Authentication, request-time authorization decisions and role/state lifecycle mutation
/// (which bumps <see cref="AuthorizationStateVersion"/> and rotates <see cref="SecurityStamp"/>)
/// are owned by later leaves and are not implemented here.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "\"Operator\" is the exact domain term fixed by specification §4.4/§6 and " +
        "ADR-0007 (\"operator management\"); renaming it would diverge from the approved contract.")]
public sealed class Operator
{
    /// <summary>Application-generated UUIDv7 primary key (ADR-0006). Never database-generated.</summary>
    public required Guid Id { get; init; }

    /// <summary>ログインに使用する識別情報 (login identifier), as originally supplied.</summary>
    public required string UserName { get; set; }

    /// <summary>
    /// Upper-invariant canonical form of <see cref="UserName"/>. Uniqueness is enforced on this
    /// column so one login identity cannot be registered twice under different casing.
    /// </summary>
    public required string NormalizedUserName { get; set; }

    /// <summary>ASP.NET Core Identity password hash (<see cref="OperatorPasswordHasher"/>). Never plaintext.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// ASP.NET Core Identity security-stamp state required by ADR-0007. A new value invalidates
    /// previously issued tokens on their next use.
    /// </summary>
    public required string SecurityStamp { get; set; }

    /// <summary>The Operator's exactly-one current fixed role (specification §6.2).</summary>
    public required OperatorRole Role { get; set; }

    /// <summary>有効 / 無効 (ADR-0006).</summary>
    public required OperatorState State { get; set; }

    /// <summary>
    /// Versioned authorization-state value required by ADR-0007, compared against the value
    /// embedded in a JWT to reject tokens issued before a role/state change.
    /// </summary>
    public required long AuthorizationStateVersion { get; set; }

    /// <summary>作成日時 (UTC, ADR-0006 §Time).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新日時 (UTC, ADR-0006 §Time).</summary>
    public required DateTimeOffset UpdatedAt { get; set; }
}
