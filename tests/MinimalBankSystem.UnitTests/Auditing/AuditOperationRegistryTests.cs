using MinimalBankSystem.Application.Auditing;

namespace MinimalBankSystem.UnitTests.Auditing;

public sealed class AuditOperationRegistryTests
{
    [Fact]
    public void RegisteredIdentifierIsAcceptedAndUnknownIdentifierFailsClosed()
    {
        AuditOperationRegistry registry = new(
            [new AuditOperationRegistration("verification.audit.registered")]);

        registry.EnsureRegistered("verification.audit.registered");
        Assert.Throws<UnregisteredAuditOperationException>(
            () => registry.EnsureRegistered("verification.audit.unregistered"));
    }

    [Fact]
    public void DuplicateRegistrationIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new AuditOperationRegistry(
            [
                new AuditOperationRegistration("verification.audit.duplicate"),
                new AuditOperationRegistration("verification.audit.duplicate"),
            ]));
    }
}
