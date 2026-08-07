using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Log events written by the representative test endpoints.
/// </summary>
internal static partial class RuntimeContractTestLog
{
    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Information,
        Message = "Representative endpoint executed.")]
    public static partial void RepresentativeEndpointExecuted(this ILogger logger);
}
