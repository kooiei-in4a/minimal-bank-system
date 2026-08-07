namespace MinimalBankSystem.IntegrationTests.TestInfrastructure;

public sealed class FixedTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 8, 3, 4, 5, TimeSpan.Zero);

    public static FixedTimeProvider Instance { get; } = new();

    public override DateTimeOffset GetUtcNow() => FixedUtcNow;

    public override long GetTimestamp() => (FixedUtcNow - DateTimeOffset.UnixEpoch).Ticks;
}
