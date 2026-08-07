namespace MinimalBankSystem.Application;

public sealed class ApplicationClock(TimeProvider timeProvider)
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
}
