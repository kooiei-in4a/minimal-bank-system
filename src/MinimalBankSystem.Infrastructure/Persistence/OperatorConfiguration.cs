using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MinimalBankSystem.Domain;

namespace MinimalBankSystem.Infrastructure.Persistence;

/// <summary>Explicit persistence mapping for the single-table Operator identity.</summary>
internal sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    private static readonly ValueConverter<OperatorState, string> StateConverter =
        new(state => ToStorage(state), value => StateFromStorage(value));

    private static readonly ValueConverter<OperatorRole, string> RoleConverter =
        new(role => ToStorage(role), value => RoleFromStorage(value));

    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable(
            "Operators",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Operators_State",
                    "\"State\" IN ('active', 'disabled')");
                table.HasCheckConstraint(
                    "CK_Operators_Role",
                    "\"Role\" IN ('administrator', 'teller', 'viewer')");
                table.HasCheckConstraint(
                    "CK_Operators_AuthorizationStateVersion",
                    "\"AuthorizationStateVersion\" > 0");
            });

        builder.HasKey(operatorIdentity => operatorIdentity.Id);
        builder.Property(operatorIdentity => operatorIdentity.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(operatorIdentity => operatorIdentity.UserName)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.NormalizedUserName)
            .HasMaxLength(256)
            .IsRequired();
        builder.HasIndex(operatorIdentity => operatorIdentity.NormalizedUserName)
            .IsUnique();

        builder.Property(operatorIdentity => operatorIdentity.PasswordHash)
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.SecurityStamp)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.ConcurrencyStamp)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(operatorIdentity => operatorIdentity.State)
            .HasConversion(StateConverter)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.Role)
            .HasConversion(RoleConverter)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.AuthorizationStateVersion)
            .HasColumnType("bigint")
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(operatorIdentity => operatorIdentity.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // ADR-0007 does not require these Identity features. Ignoring them prevents the default
        // AspNetUsers surface from silently becoming part of the product schema.
        builder.Ignore(operatorIdentity => operatorIdentity.Email);
        builder.Ignore(operatorIdentity => operatorIdentity.NormalizedEmail);
        builder.Ignore(operatorIdentity => operatorIdentity.EmailConfirmed);
        builder.Ignore(operatorIdentity => operatorIdentity.PhoneNumber);
        builder.Ignore(operatorIdentity => operatorIdentity.PhoneNumberConfirmed);
        builder.Ignore(operatorIdentity => operatorIdentity.TwoFactorEnabled);
        builder.Ignore(operatorIdentity => operatorIdentity.LockoutEnd);
        builder.Ignore(operatorIdentity => operatorIdentity.LockoutEnabled);
        builder.Ignore(operatorIdentity => operatorIdentity.AccessFailedCount);
    }

    private static string ToStorage(OperatorState state) => state switch
    {
        OperatorState.Active => "active",
        OperatorState.Disabled => "disabled",
        _ => throw new InvalidOperationException($"Unsupported Operator state: {state}"),
    };

    private static OperatorState StateFromStorage(string value) => value switch
    {
        "active" => OperatorState.Active,
        "disabled" => OperatorState.Disabled,
        _ => throw new InvalidOperationException($"Unsupported persisted Operator state: {value}"),
    };

    private static string ToStorage(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => "administrator",
        OperatorRole.Teller => "teller",
        OperatorRole.Viewer => "viewer",
        _ => throw new InvalidOperationException($"Unsupported Operator role: {role}"),
    };

    private static OperatorRole RoleFromStorage(string value) => value switch
    {
        "administrator" => OperatorRole.Administrator,
        "teller" => OperatorRole.Teller,
        "viewer" => OperatorRole.Viewer,
        _ => throw new InvalidOperationException($"Unsupported persisted Operator role: {value}"),
    };
}
