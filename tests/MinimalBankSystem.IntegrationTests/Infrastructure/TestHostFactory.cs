using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MinimalBankSystem.Api.Extensions;
using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.IntegrationTests.Infrastructure;

internal static class TestHostFactory
{
    public static IHost Build(
        Action<IServiceCollection>? configureServices = null,
        Action<IEndpointRouteBuilder>? mapExtra = null)
    {
        IHostBuilder hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMinimalBankSystemRuntime();
                        configureServices?.Invoke(services);
                    })
                    .ConfigureLogging((_, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddJsonConsole(opts => opts.IncludeScopes = true);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseMinimalBankSystemRuntime();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/_contract/echo", (HttpContext context) =>
                                Results.Ok(new
                                {
                                    CorrelationId = context.Items[TimeProviderKeys.CorrelationIdItemKey] as string,
                                }));

                            endpoints.MapGet("/_contract/time", (TimeProvider timeProvider) =>
                                Results.Ok(new { UtcNow = timeProvider.GetUtcNow() }));

                            endpoints.MapGet("/_contract/boom", () =>
                            {
                                throw new InvalidOperationException("boom: secret-detail-should-not-leak");
                            });

                            mapExtra?.Invoke(endpoints);
                        });
                    });
            });

        return hostBuilder.Start();
    }
}
