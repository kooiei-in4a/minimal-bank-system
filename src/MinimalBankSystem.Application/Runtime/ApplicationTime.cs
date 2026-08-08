namespace MinimalBankSystem.Application.Runtime;

public sealed class ApplicationTime(TimeProvider timeProvider)
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
}
