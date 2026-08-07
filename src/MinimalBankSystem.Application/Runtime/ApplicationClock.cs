namespace MinimalBankSystem.Application.Runtime;

public sealed class ApplicationClock
{
    private readonly TimeProvider _timeProvider;

    public ApplicationClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
}
