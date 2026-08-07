namespace MinimalBankSystem.Application.Diagnostics;

public sealed class CurrentTimeReader(TimeProvider timeProvider)
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
}
