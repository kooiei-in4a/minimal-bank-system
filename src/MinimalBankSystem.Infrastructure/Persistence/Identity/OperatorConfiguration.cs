using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence.Identity;

internal sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable(
            OperatorPersistence.TableName,
            table =>
            {
                table.HasCheckConstraint(
                    OperatorPersistence.StateCheckConstraint,
                    $"{OperatorPersistence.StateColumn} IN ('{OperatorPersistence.ActiveStateToken}', '{OperatorPersistence.DisabledStateToken}')");
                table.HasCheckConstraint(
                    OperatorPersistence.RoleCheckConstraint,
                    $"{OperatorPersistence.FixedRoleColumn} IN ('{OperatorPersistence.AdministratorRoleToken}', '{OperatorPersistence.TellerRoleToken}', '{OperatorPersistence.ViewerRoleToken}')");
            });

        builder.HasKey(operatorEntity => operatorEntity.Id);
        builder.Property(operatorEntity => operatorEntity.Id)
            .HasColumnName(OperatorPersistence.IdColumn)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(operatorEntity => operatorEntity.UserName)
            .HasColumnName(OperatorPersistence.UserNameColumn)
            .HasColumnType("text")
            .HasMaxLength(Operator.UserNameMaxLength)
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.NormalizedUserName)
            .HasColumnName(OperatorPersistence.NormalizedUserNameColumn)
            .HasColumnType("text")
            .HasMaxLength(Operator.UserNameMaxLength)
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.PasswordHash)
            .HasColumnName(OperatorPersistence.PasswordHashColumn)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.SecurityStamp)
            .HasColumnName(OperatorPersistence.SecurityStampColumn)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.State)
            .HasColumnName(OperatorPersistence.StateColumn)
            .HasColumnType("text")
            .HasConversion(
                state => ToStateToken(state),
                token => FromStateToken(token))
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.Role)
            .HasColumnName(OperatorPersistence.FixedRoleColumn)
            .HasColumnType("text")
            .HasConversion(
                role => ToRoleToken(role),
                token => FromRoleToken(token))
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.AuthorizationStateVersion)
            .HasColumnName(OperatorPersistence.AuthorizationStateVersionColumn)
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.CreatedAt)
            .HasColumnName(OperatorPersistence.CreatedAtColumn)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.UpdatedAt)
            .HasColumnName(OperatorPersistence.UpdatedAtColumn)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(operatorEntity => operatorEntity.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName(OperatorPersistence.NormalizedUserNameIndex);
    }

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => OperatorPersistence.ActiveStateToken,
        OperatorState.Disabled => OperatorPersistence.DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Operator state."),
    };

    private static OperatorState FromStateToken(string token) => token switch
    {
        OperatorPersistence.ActiveStateToken => OperatorState.Active,
        OperatorPersistence.DisabledStateToken => OperatorState.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unrecognized persisted Operator state."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Operator role."),
    };

    private static OperatorRole FromRoleToken(string token) => token switch
    {
        OperatorPersistence.AdministratorRoleToken => OperatorRole.Administrator,
        OperatorPersistence.TellerRoleToken => OperatorRole.Teller,
        OperatorPersistence.ViewerRoleToken => OperatorRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unrecognized persisted Operator role."),
    };
}
