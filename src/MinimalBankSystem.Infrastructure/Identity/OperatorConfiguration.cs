using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MinimalBankSystem.Infrastructure.Identity;

/// <summary>
/// EF Core mapping for <see cref="Operator"/>.
/// </summary>
/// <remarks>
/// Follows the repository's established "C# enum, PostgreSQL stable lowercase text plus CHECK
/// constraint" pattern (ADR-0006) for <see cref="OperatorState"/> and <see cref="OperatorRole"/>,
/// rather than a PostgreSQL native enum or a role join table. A single NOT NULL scalar column is
/// the only shape that can physically guarantee "exactly one current role": zero roles and
/// multiple simultaneous roles are both structurally impossible to represent, not merely
/// application-checked. Fixed-role definitions therefore have no CRUD table of their own.
/// </remarks>
public sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public const string TableName = "Operators";

    public const string ActiveStateToken = "active";
    public const string DisabledStateToken = "disabled";

    public const string AdministratorRoleToken = "administrator";
    public const string TellerRoleToken = "teller";
    public const string ViewerRoleToken = "viewer";

    /// <summary>
    /// Never satisfies <see cref="RoleCheckConstraintName"/>. Reachable only by attempting to
    /// persist an <see cref="OperatorRole.Unspecified"/> Operator, which is exactly the
    /// "zero current roles" rejection path required by Issue #165.
    /// </summary>
    public const string UnspecifiedRoleToken = "unspecified";

    public const string StateCheckConstraintName = "CK_Operators_State";
    public const string RoleCheckConstraintName = "CK_Operators_Role";
    public const string NormalizedUserNameIndexName = "IX_Operators_NormalizedUserName";

    private static readonly ValueConverter<OperatorState, string> StateConverter = new(
        state => ToToken(state),
        token => ToState(token));

    private static readonly ValueConverter<OperatorRole, string> RoleConverter = new(
        role => ToToken(role),
        token => ToRole(token));

    private static string ToToken(OperatorState state) => state switch
    {
        OperatorState.Active => ActiveStateToken,
        OperatorState.Disabled => DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Operator state."),
    };

    private static OperatorState ToState(string token) => token switch
    {
        ActiveStateToken => OperatorState.Active,
        DisabledStateToken => OperatorState.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unrecognized persisted Operator state."),
    };

    // Unspecified deliberately maps to a token outside the CHECK constraint's allowed set instead
    // of throwing, so persisting an Unspecified role fails at the database as a real constraint
    // violation (the "zero current roles" proof) rather than earlier as a C# exception.
    private static string ToToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => AdministratorRoleToken,
        OperatorRole.Teller => TellerRoleToken,
        OperatorRole.Viewer => ViewerRoleToken,
        _ => UnspecifiedRoleToken,
    };

    private static OperatorRole ToRole(string token) => token switch
    {
        AdministratorRoleToken => OperatorRole.Administrator,
        TellerRoleToken => OperatorRole.Teller,
        ViewerRoleToken => OperatorRole.Viewer,
        _ => OperatorRole.Unspecified,
    };

    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint(
                StateCheckConstraintName,
                $"\"{nameof(Operator.State)}\" IN ('{ActiveStateToken}', '{DisabledStateToken}')");
            table.HasCheckConstraint(
                RoleCheckConstraintName,
                $"\"{nameof(Operator.Role)}\" IN ('{AdministratorRoleToken}', '{TellerRoleToken}', '{ViewerRoleToken}')");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.HasIndex(o => o.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName(NormalizedUserNameIndexName);

        builder.Property(o => o.State)
            .HasConversion(StateConverter);

        builder.Property(o => o.Role)
            .HasConversion(RoleConverter);

        builder.Property(o => o.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(o => o.UpdatedAt)
            .HasColumnType("timestamptz");
    }
}
