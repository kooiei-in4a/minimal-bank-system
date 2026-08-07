using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.UnitTests;

public sealed class ApplicationTimeTests
{
    [Fact]
    public void GetUtcNowUsesInjectedTimeProvider()
    {
        DateTimeOffset expected = new(2026, 8, 8, 9, 15, 30, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(expected);
        ApplicationTime applicationTime = new(timeProvider);

        Assert.Equal(expected, applicationTime.GetUtcNow());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
