namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> used to prove that application code reads time from the
/// injected provider instead of the system clock.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
