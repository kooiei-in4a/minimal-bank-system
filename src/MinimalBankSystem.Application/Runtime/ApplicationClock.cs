namespace MinimalBankSystem.Application.Runtime;

public sealed class ApplicationClock
{
    private readonly TimeProvider timeProvider;

    public ApplicationClock(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
    }

    public DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow();
    }
}
