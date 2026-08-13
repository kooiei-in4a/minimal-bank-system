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
                    $"{OperatorPersistence.StateColumn} IN ('active', 'disabled')");
                table.HasCheckConstraint(
                    OperatorPersistence.RoleCheckConstraint,
                    $"{OperatorPersistence.FixedRoleColumn} IN ('administrator', 'teller', 'viewer')");
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
            .HasConversion(LowercaseEnumConverter.Create<OperatorState>())
            .IsRequired();

        builder.Property(operatorEntity => operatorEntity.Role)
            .HasColumnName(OperatorPersistence.FixedRoleColumn)
            .HasColumnType("text")
            .HasConversion(LowercaseEnumConverter.Create<OperatorRole>())
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

        builder.HasIndex(operatorEntity => operatorEntity.UserName)
            .IsUnique()
            .HasDatabaseName(OperatorPersistence.UserNameIndex);

        builder.HasIndex(operatorEntity => operatorEntity.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName(OperatorPersistence.NormalizedUserNameIndex);
    }
}

internal static class LowercaseEnumConverter
{
    public static ValueConverter<TEnum, string> Create<TEnum>()
        where TEnum : struct, Enum =>
        new(
            value => value.ToString().ToLowerInvariant(),
            stored => Enum.Parse<TEnum>(stored, ignoreCase: true));
}
