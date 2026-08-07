using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.IntegrationTests.Infrastructure;

namespace MinimalBankSystem.IntegrationTests.Api;

public sealed class ApiContractWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly CollectingLoggerProvider _loggerProvider = new();

    public TimeProvider? TimeProviderOverride { get; init; }

    public CollectingLoggerProvider LoggerProvider => _loggerProvider;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EnableApiContractProbes"] = "true",
                });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "O";
                options.UseUtcTimestamp = true;
            });
            logging.AddProvider(_loggerProvider);
            logging.SetMinimumLevel(LogLevel.Information);
        });

        builder.ConfigureServices(services =>
        {
            if (TimeProviderOverride is null)
            {
                return;
            }

            services.RemoveAll<TimeProvider>();
            services.AddSingleton(TimeProviderOverride);
        });
    }
}
