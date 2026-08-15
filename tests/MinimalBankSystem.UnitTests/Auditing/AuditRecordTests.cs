using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditRecordTests
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2035, 7, 8, 9, 10, 11, TimeSpan.Zero);

    [Fact]
    public void CreateUsesUuidV7UtcAndTheExactApprovedLogicalFields()
    {
        Guid actorId = Guid.CreateVersion7(FrozenUtc);
        AuditRecord record = AuditRecord.Create(
            actorId,
            OperatorRole.Administrator,
            "operator.role.change",
            $"operator:{actorId:D}",
            AuditResult.Failure,
            "operator_disabled",
            "audit-unit-correlation",
            FrozenUtc.ToOffset(TimeSpan.FromHours(9)));

        Assert.Equal(7, record.AuditId.Version);
        Assert.Equal(actorId, record.ActorIdentifier);
        Assert.Equal(OperatorRole.Administrator, record.ActorRole);
        Assert.Equal("operator.role.change", record.OperationIdentifier);
        Assert.Equal($"operator:{actorId:D}", record.TargetIdentifier);
        Assert.Equal(AuditResult.Failure, record.Result);
        Assert.Equal("operator_disabled", record.FailureBusinessErrorCode);
        Assert.Equal("audit-unit-correlation", record.CorrelationId);
        Assert.Equal(FrozenUtc, record.AuditTime);
        Assert.Equal(TimeSpan.Zero, record.AuditTime.Offset);
    }

    [Fact]
    public void ResultAndIdentifierValidationFailsClosed()
    {
        Guid actorId = Guid.CreateVersion7(FrozenUtc);

        Assert.ThrowsAny<ArgumentException>(() => AuditRecord.Create(
            actorId,
            OperatorRole.Viewer,
            "operation.success",
            "account:1",
            AuditResult.Success,
            "must-not-exist",
            "correlation",
            FrozenUtc));
        Assert.ThrowsAny<ArgumentException>(() => AuditRecord.Create(
            actorId,
            OperatorRole.Viewer,
            "operation.failure",
            "account:1",
            AuditResult.Failure,
            failureBusinessErrorCode: null,
            "correlation",
            FrozenUtc));
        Assert.ThrowsAny<ArgumentException>(() => AuditRecord.Create(
            actorId,
            OperatorRole.Viewer,
            "operation with spaces",
            "account:1",
            AuditResult.Success,
            failureBusinessErrorCode: null,
            "correlation",
            FrozenUtc));
    }
}
