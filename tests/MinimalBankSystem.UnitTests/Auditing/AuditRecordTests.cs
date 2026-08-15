using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditRecordTests
{
    [Fact]
    public void CreateGeneratesUuidV7AndNormalizesTheInjectedInstantToUtc()
    {
        DateTimeOffset supplied = new(2026, 8, 15, 8, 30, 45, TimeSpan.FromHours(9));
        Guid actor = Guid.CreateVersion7(supplied);

        AuditRecord record = AuditRecord.Create(
            actor,
            OperatorRole.Viewer,
            "verification.audit.query",
            "operator-42",
            AuditResult.Failure,
            "operator_not_found",
            "correlation-audit-42",
            supplied);

        Assert.Equal(7, record.AuditId.Version);
        Assert.Equal(actor, record.ActorIdentifier);
        Assert.Equal(OperatorRole.Viewer, record.ActorRole);
        Assert.Equal("verification.audit.query", record.OperationIdentifier);
        Assert.Equal("operator-42", record.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, record.Result);
        Assert.Equal("operator_not_found", record.FailureBusinessErrorCode);
        Assert.Equal("correlation-audit-42", record.CorrelationId);
        Assert.Equal(TimeSpan.Zero, record.AuditTime.Offset);
        Assert.Equal(supplied.ToUniversalTime(), record.AuditTime);
    }

    [Fact]
    public void SuccessRejectsAFailureBusinessErrorCode()
    {
        Assert.Throws<ArgumentException>(() => AuditRecord.Create(
            Guid.CreateVersion7(),
            OperatorRole.Teller,
            "verification.audit.success",
            "account-42",
            AuditResult.Success,
            "must_not_be_present",
            "correlation-audit-success",
            DateTimeOffset.UtcNow));
    }
}
