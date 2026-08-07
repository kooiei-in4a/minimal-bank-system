using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.UnitTests;

public sealed class ApplicationTimeTests
{
    [Fact]
    public void GetUtcNowUsesInjectedTimeProvider()
    {
        DateTimeOffset fixedUtc = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedUtc);
        var applicationTime = new ApplicationTime(timeProvider);

        Assert.Equal(fixedUtc, applicationTime.GetUtcNow());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
