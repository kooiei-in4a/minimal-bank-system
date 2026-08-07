namespace MinimalBankSystem.Application.Time;

/// <summary>
/// Application-facing clock that reads time only from an injected <see cref="TimeProvider"/>.
/// </summary>
public sealed class ApplicationTime
{
    private readonly TimeProvider _timeProvider;

    public ApplicationTime(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();
}
