using MinimalBankSystem.Application.Runtime;

namespace MinimalBankSystem.UnitTests;

public sealed class ApplicationClockTests
{
    [Fact]
    public void UsesTheInjectedTimeProvider()
    {
        DateTimeOffset expected = new(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);
        ApplicationClock clock = new(new FixedTimeProvider(expected));

        Assert.Equal(expected, clock.UtcNow);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
