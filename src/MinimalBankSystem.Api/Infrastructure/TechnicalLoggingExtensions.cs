using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Api.Infrastructure;

public static class TechnicalLoggingExtensions
{
    public static ILoggingBuilder AddTechnicalLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options => options.IncludeScopes = true);
        return logging;
    }
}
