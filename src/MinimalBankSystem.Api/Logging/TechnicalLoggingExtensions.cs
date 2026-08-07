using System.Text.Json;

namespace MinimalBankSystem.Api.Logging;

public static class TechnicalLoggingExtensions
{
    public static ILoggingBuilder AddTechnicalJsonConsoleLogging(this ILoggingBuilder builder)
    {
        builder.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
        });

        return builder;
    }
}
