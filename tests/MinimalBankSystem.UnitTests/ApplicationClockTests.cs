using MinimalBankSystem.Application.Runtime;

namespace MinimalBankSystem.UnitTests;

public sealed class ApplicationClockTests
{
    [Fact]
    public void GetUtcNowUsesInjectedTimeProvider()
    {
        DateTimeOffset expectedUtcNow = new(2026, 8, 8, 12, 34, 56, TimeSpan.Zero);
        ApplicationClock clock = new(new FixedTimeProvider(expectedUtcNow));

        DateTimeOffset actualUtcNow = clock.GetUtcNow();

        Assert.Equal(expectedUtcNow, actualUtcNow);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
