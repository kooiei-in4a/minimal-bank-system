namespace MinimalBankSystem.Application.Time;

public static class TimeProviderKeys
{
    public const string CorrelationIdItemKey = "CorrelationId";
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const int MaxCorrelationIdLength = 128;
}
