namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>
/// The three fixed Operator roles from specification §6.2. Role definitions themselves are not
/// runtime CRUD-managed; this enum is closed and is not expected to grow without a new ADR.
/// </summary>
public enum OperatorRole
{
    /// <summary>
    /// Deliberately not a valid persisted role. An Operator constructed without an explicit role
    /// keeps this value, which fails <see cref="OperatorConfiguration"/>'s check constraint instead
    /// of silently persisting as the first declared real role. This is the "zero current roles"
    /// rejection path required by Issue #165.
    /// </summary>
    Unspecified = 0,

    /// <summary>管理者 (Administrator).</summary>
    Administrator,

    /// <summary>窓口担当者 (Teller).</summary>
    Teller,

    /// <summary>閲覧者 (Viewer).</summary>
    Viewer,
}
