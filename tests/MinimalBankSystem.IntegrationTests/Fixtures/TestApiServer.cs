using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Extensions;

namespace MinimalBankSystem.IntegrationTests.Fixtures;

public sealed class TestApiServer : IDisposable
{
    private readonly TestServer _server;

    public TestApiServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMinimalBankSystemApi();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddJsonConsole();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMinimalBankSystemApi();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/contract/echo", async (HttpContext context) =>
                    {
                        string? correlationId = context.Items["CorrelationId"] as string;
                        await context.Response.WriteAsJsonAsync(new { CorrelationId = correlationId });
                    });

                    endpoints.MapGet("/api/contract/error", () =>
                    {
                        throw new InvalidOperationException("Test unmapped exception");
                    });

                    endpoints.MapGet("/api/contract/time", (TimeProvider timeProvider) =>
                    {
                        DateTimeOffset now = timeProvider.GetUtcNow();
                        return Results.Ok(new { UtcNow = now });
                    });
                });
            });

        _server = new TestServer(builder);
    }

    private TestApiServer(TestServer server)
    {
        _server = server;
    }

    public HttpClient Client => _server.CreateClient();

    public TestServer Server => _server;

    public static TestApiServer CreateWithCustomServices(Action<IServiceCollection> configureServices)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMinimalBankSystemApi();
                configureServices(services);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddJsonConsole();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMinimalBankSystemApi();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/contract/echo", async (HttpContext context) =>
                    {
                        string? correlationId = context.Items["CorrelationId"] as string;
                        await context.Response.WriteAsJsonAsync(new { CorrelationId = correlationId });
                    });

                    endpoints.MapGet("/api/contract/error", () =>
                    {
                        throw new InvalidOperationException("Test unmapped exception");
                    });

                    endpoints.MapGet("/api/contract/time", (TimeProvider timeProvider) =>
                    {
                        DateTimeOffset now = timeProvider.GetUtcNow();
                        return Results.Ok(new { UtcNow = now });
                    });
                });
            });

        return new TestApiServer(new TestServer(builder));
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
