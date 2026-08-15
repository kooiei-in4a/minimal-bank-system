using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditOperationRegistryTests
{
    [Fact]
    public void ExplicitFeatureContributionIsAcceptedWhileUnknownValuesFailClosed()
    {
        AuditOperationRegistry registry = new(
            [new AuditOperationRegistration("verification.operation")]);

        registry.EnsureRegistered("verification.operation");
        Assert.Throws<UnregisteredAuditOperationException>(
            () => registry.EnsureRegistered("Verification.Operation"));
        Assert.Throws<UnregisteredAuditOperationException>(
            () => new AuditOperationRegistry([]).EnsureRegistered("verification.operation"));
    }

    [Fact]
    public void RegistrySnapshotsContributionsAndRejectsDuplicates()
    {
        List<AuditOperationRegistration> registrations =
            [new("feature.first")];
        AuditOperationRegistry registry = new(registrations);

        registrations.Add(new AuditOperationRegistration("feature.late"));

        registry.EnsureRegistered("feature.first");
        Assert.Throws<UnregisteredAuditOperationException>(
            () => registry.EnsureRegistered("feature.late"));
        Assert.Throws<InvalidOperationException>(() => new AuditOperationRegistry(
            [new("duplicate"), new("duplicate")]));
    }

    [Fact]
    public void RegistrationRejectsWhitespaceAndOversizedIdentifiers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuditOperationRegistration(" padded"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuditOperationRegistration(
                new string('x', AuditRecord.OperationIdentifierMaxLength + 1)));
    }
}
