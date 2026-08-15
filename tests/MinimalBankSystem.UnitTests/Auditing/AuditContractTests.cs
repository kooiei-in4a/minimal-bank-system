using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditContractTests
{
    private static readonly DateTimeOffset FrozenUtc =
        new(2032, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void AuditRecordCapturesTheApprovedImmutableLogicalFields()
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
    public void SuccessfulRecordRejectsAFailureBusinessErrorCode()
    {
        Assert.Throws<ArgumentException>(() => AuditRecord.Create(
            Guid.NewGuid(),
            OperatorRole.Administrator,
            "verification.operation",
            "operator:01",
            AuditResult.Success,
            "must_not_be_present",
            "correlation-02",
            FrozenUtc));
    }

    [Fact]
    public void RegistryIsAnExplicitOrdinalAllowlistAndFailsClosed()
    {
        AuditOperationRegistry registry = new(["verification.operation"]);

        registry.EnsureRegistered("verification.operation");
        Assert.Throws<UnregisteredAuditOperationException>(
            () => registry.EnsureRegistered("Verification.Operation"));
        Assert.Throws<UnregisteredAuditOperationException>(
            () => AuditOperationRegistry.Empty.EnsureRegistered("verification.operation"));
    }

    [Fact]
    public void RegistryRejectsDuplicateAndOversizedIdentifiers()
    {
        Assert.Throws<ArgumentException>(
            () => new AuditOperationRegistry(["duplicate", "duplicate"]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuditOperationRegistry([new string('x', AuditRecord.OperationIdentifierMaxLength + 1)]));
    }
}
