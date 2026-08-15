using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditRecordTests
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2032, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void CreateCapturesTheApprovedSnapshotWithUuidV7AndUtcTime()
    {
        Guid actor = Guid.NewGuid();

        AuditRecord record = AuditRecord.Create(
            actor,
            OperatorRole.Teller,
            "verification.operation",
            "account:000000000123",
            AuditResult.Failure,
            "verification_rejected",
            "correlation-01",
            FrozenUtc.ToOffset(TimeSpan.FromHours(9)));

        Assert.Equal(7, record.Id.Version);
        Assert.Equal(actor, record.ActorIdentifier);
        Assert.Equal(OperatorRole.Teller, record.ActorRole);
        Assert.Equal("verification.operation", record.OperationIdentifier);
        Assert.Equal("account:000000000123", record.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, record.Result);
        Assert.Equal("verification_rejected", record.FailureBusinessErrorCode);
        Assert.Equal("correlation-01", record.CorrelationId);
        Assert.Equal(FrozenUtc, record.AuditTime);
        Assert.Equal(TimeSpan.Zero, record.AuditTime.Offset);
    }

    [Fact]
    public void FailureCodeIsOptionalForFailuresButForbiddenForSuccess()
    {
        AuditRecord failureWithoutApplicableCode = AuditRecord.Create(
            Guid.NewGuid(),
            OperatorRole.Viewer,
            "verification.failure",
            "operator:01",
            AuditResult.Failure,
            failureBusinessErrorCode: null,
            "correlation-02",
            FrozenUtc);

        Assert.Null(failureWithoutApplicableCode.FailureBusinessErrorCode);
        Assert.Throws<ArgumentException>(() => AuditRecord.Create(
            Guid.NewGuid(),
            OperatorRole.Administrator,
            "verification.success",
            "operator:01",
            AuditResult.Success,
            "must_not_be_present",
            "correlation-03",
            FrozenUtc));
    }

    [Fact]
    public void CorrelationIdentifierUsesTheExistingSixtyFourCharacterContract()
    {
        AuditRecord accepted = AuditRecord.Create(
            Guid.NewGuid(),
            OperatorRole.Viewer,
            "verification.correlation",
            "operator:01",
            AuditResult.Success,
            failureBusinessErrorCode: null,
            new string('c', 64),
            FrozenUtc);

        Assert.Equal(64, accepted.CorrelationId.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditRecord.Create(
            Guid.NewGuid(),
            OperatorRole.Viewer,
            "verification.correlation",
            "operator:01",
            AuditResult.Success,
            failureBusinessErrorCode: null,
            new string('c', 65),
            FrozenUtc));
    }
}
