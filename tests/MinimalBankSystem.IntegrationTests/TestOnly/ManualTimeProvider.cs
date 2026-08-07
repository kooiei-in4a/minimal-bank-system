namespace MinimalBankSystem.IntegrationTests.TestOnly;

internal sealed class ManualTimeProvider(DateTimeOffset fixedTime) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => fixedTime;
}
