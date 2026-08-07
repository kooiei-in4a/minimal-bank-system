using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MinimalBankSystem.Api.Extensions;
using Xunit;
using System.Linq;

namespace MinimalBankSystem.IntegrationTests.Logging;

public sealed class JsonConsoleLoggingTests
{
    [Fact]
    public void AddJsonConsoleRegistersConsoleLoggerProviderInHost()
    {
        using IHost host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMinimalBankSystemRuntime();
                    })
                    .ConfigureLogging((_, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddJsonConsole();
                    })
                    .Configure(_ => { });
            })
            .Build();

        IServiceProvider services = host.Services;
        ILoggerProvider[] providers = services.GetServices<ILoggerProvider>().ToArray();
        bool hasConsoleProvider = providers.OfType<ConsoleLoggerProvider>().Any();
        Assert.True(hasConsoleProvider, "ConsoleLoggerProvider (AddJsonConsole) is not registered in the host.");
    }

    [Fact]
    public void LoggingBuilderCanIncludeScopesForCorrelationPropagation()
    {
        using IHost host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMinimalBankSystemRuntime();
                    })
                    .ConfigureLogging((_, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddJsonConsole(opts => opts.IncludeScopes = true);
                    })
                    .Configure(_ => { });
            })
            .Build();

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Probe");
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = "trace-abc" });

        // Scope disposal must not throw and a logger call inside a scope must succeed.
        logger.LogInformation("scoped message");

        Assert.NotNull(scope);
    }
}
