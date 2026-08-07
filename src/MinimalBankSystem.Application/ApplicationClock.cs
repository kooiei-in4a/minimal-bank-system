namespace MinimalBankSystem.Application;

public interface IApplicationClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class ApplicationClock(TimeProvider timeProvider) : IApplicationClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
