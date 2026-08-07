using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Request level API integration test host.
/// </summary>
/// <remarks>
/// The host composes exactly the production entry points of the common API runtime contract
/// (<see cref="ApiTechnicalLoggingExtensions.AddApiTechnicalLogging"/>,
/// <see cref="ApiRuntimeServiceCollectionExtensions.AddApiRuntimeContract"/> and
/// <see cref="ApiRuntimeApplicationBuilderExtensions.UseApiRuntimeContract"/>) and adds only the
/// representative test endpoints on top, so the tests observe the real pipeline.
/// </remarks>
internal sealed class ApiRuntimeTestServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly ConsoleOutputCapture _consoleOutput;
    private string? _consoleLog;

    private ApiRuntimeTestServer(WebApplication application, ConsoleOutputCapture consoleOutput)
    {
        _application = application;
        _consoleOutput = consoleOutput;
        Client = application.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<ApiRuntimeTestServer> StartAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        ConsoleOutputCapture consoleOutput = ConsoleOutputCapture.Start();

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.AddApiTechnicalLogging();

            builder.Services.AddApiRuntimeContract();
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(RuntimeContractTestController).Assembly);

            configureServices?.Invoke(builder.Services);

            WebApplication application = builder.Build();
            application.UseApiRuntimeContract();
            application.MapControllers();

            await application.StartAsync();

            return new ApiRuntimeTestServer(application, consoleOutput);
        }
        catch
        {
            consoleOutput.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Stops the host so the console logger drains its queue, then returns the captured console
    /// stream.
    /// </summary>
    public async Task<string> StopAndReadConsoleLogAsync()
    {
        if (_consoleLog is null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
            _consoleLog = _consoleOutput.StopAndRead();
        }

        return _consoleLog;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAndReadConsoleLogAsync();
        Client.Dispose();
    }
}
