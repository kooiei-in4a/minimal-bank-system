namespace MinimalBankSystem.Application.Time;

public sealed class ApplicationTime
{
    private readonly TimeProvider _timeProvider;

    public ApplicationTime(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();
}
