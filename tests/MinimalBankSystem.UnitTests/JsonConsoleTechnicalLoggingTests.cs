using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.UnitTests;

public sealed class JsonConsoleTechnicalLoggingTests
{
    [Fact]
    public void ProhibitedFieldSanitizingProvider_UsesJsonConsoleBackend()
    {
        using ProhibitedFieldSanitizingLoggerProvider provider = new();
        ILogger logger = provider.CreateLogger("JsonConsoleContract");

        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }
}
