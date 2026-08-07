namespace MinimalBankSystem.IntegrationTests;

public sealed class TimeProviderTests
{
    [Fact]
    public void SystemTimeProviderReturnsReasonableTime()
    {
        TimeProvider timeProvider = TimeProvider.System;
        DateTimeOffset before = DateTimeOffset.UtcNow;

        DateTimeOffset result = timeProvider.GetUtcNow();

        DateTimeOffset after = DateTimeOffset.UtcNow;
        Assert.InRange(result, before.AddSeconds(-5), after.AddSeconds(5));
    }

    [Fact]
    public void CustomTimeProviderReturnsInjectedTime()
    {
        DateTimeOffset expected = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        TimeProvider fakeProvider = new FakeTimeProvider(expected);

        DateTimeOffset result = fakeProvider.GetUtcNow();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TimeProviderCanBeReplacedForTesting()
    {
        DateTimeOffset fixedTime = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        TimeProvider fakeProvider = new FakeTimeProvider(fixedTime);

        Assert.Equal(fixedTime, fakeProvider.GetUtcNow());
        Assert.Equal(fixedTime, fakeProvider.GetLocalNow());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime;

        public FakeTimeProvider(DateTimeOffset fixedTime)
        {
            _fixedTime = fixedTime;
        }

        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }
}
