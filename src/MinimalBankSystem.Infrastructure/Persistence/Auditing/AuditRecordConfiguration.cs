using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable(
            AuditPersistence.TableName,
            table =>
            {
                table.HasCheckConstraint(
                    AuditPersistence.ActorRoleCheckConstraint,
                    $"{AuditPersistence.ActorRoleColumn} IN ('{AuditPersistence.AdministratorRoleToken}', '{AuditPersistence.TellerRoleToken}', '{AuditPersistence.ViewerRoleToken}')");
                table.HasCheckConstraint(
                    AuditPersistence.ResultCheckConstraint,
                    $"{AuditPersistence.ResultColumn} IN ('{AuditPersistence.SuccessResultToken}', '{AuditPersistence.FailureResultToken}')");
                table.HasCheckConstraint(
                    AuditPersistence.FailureCodeCheckConstraint,
                    $"({AuditPersistence.ResultColumn} = '{AuditPersistence.SuccessResultToken}' AND {AuditPersistence.FailureBusinessErrorCodeColumn} IS NULL) OR {AuditPersistence.ResultColumn} = '{AuditPersistence.FailureResultToken}'");
            });

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName(AuditPersistence.IdColumn)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(record => record.ActorIdentifier)
            .HasColumnName(AuditPersistence.ActorIdentifierColumn)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(record => record.ActorRole)
            .HasColumnName(AuditPersistence.ActorRoleColumn)
            .HasColumnType("text")
            .HasConversion(
                role => ToRoleToken(role),
                token => FromRoleToken(token))
            .IsRequired();

        builder.Property(record => record.OperationIdentifier)
            .HasColumnName(AuditPersistence.OperationIdentifierColumn)
            .HasColumnType("text")
            .HasMaxLength(AuditRecord.OperationIdentifierMaxLength)
            .IsRequired();

        builder.Property(record => record.TargetIdentifier)
            .HasColumnName(AuditPersistence.TargetIdentifierColumn)
            .HasColumnType("text")
            .HasMaxLength(AuditRecord.TargetIdentifierMaxLength)
            .IsRequired();

        builder.Property(record => record.Result)
            .HasColumnName(AuditPersistence.ResultColumn)
            .HasColumnType("text")
            .HasConversion(
                result => ToResultToken(result),
                token => FromResultToken(token))
            .IsRequired();

        builder.Property(record => record.FailureBusinessErrorCode)
            .HasColumnName(AuditPersistence.FailureBusinessErrorCodeColumn)
            .HasColumnType("text")
            .HasMaxLength(AuditRecord.BusinessErrorCodeMaxLength);

        builder.Property(record => record.CorrelationId)
            .HasColumnName(AuditPersistence.CorrelationIdColumn)
            .HasColumnType("text")
            .HasMaxLength(AuditRecord.CorrelationIdMaxLength)
            .IsRequired();

        builder.Property(record => record.AuditTime)
            .HasColumnName(AuditPersistence.AuditTimeColumn)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => AuditPersistence.AdministratorRoleToken,
        OperatorRole.Teller => AuditPersistence.TellerRoleToken,
        OperatorRole.Viewer => AuditPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown audited actor role."),
    };

    private static OperatorRole FromRoleToken(string token) => token switch
    {
        AuditPersistence.AdministratorRoleToken => OperatorRole.Administrator,
        AuditPersistence.TellerRoleToken => OperatorRole.Teller,
        AuditPersistence.ViewerRoleToken => OperatorRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unrecognized audited actor role."),
    };

    private static string ToResultToken(AuditResult result) => result switch
    {
        AuditResult.Success => AuditPersistence.SuccessResultToken,
        AuditResult.Failure => AuditPersistence.FailureResultToken,
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown Audit result."),
    };

    private static AuditResult FromResultToken(string token) => token switch
    {
        AuditPersistence.SuccessResultToken => AuditResult.Success,
        AuditPersistence.FailureResultToken => AuditResult.Failure,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unrecognized persisted Audit result."),
    };
}
