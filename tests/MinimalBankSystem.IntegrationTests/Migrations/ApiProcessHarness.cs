using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace MinimalBankSystem.IntegrationTests.Migrations;

internal sealed class ApiProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> standardOutput;
    private readonly Task<string> standardError;
    private static readonly HttpClient HttpClient = new();
    private bool stopped;

    private ApiProcess(Process process, int port)
    {
        this.process = process;
        Port = port;
        standardOutput = process.StandardOutput.ReadToEndAsync();
        standardError = process.StandardError.ReadToEndAsync();
    }

    public int Port { get; }

    public string StandardOutput => standardOutput.GetAwaiter().GetResult();

    public string StandardError => standardError.GetAwaiter().GetResult();

    public static async Task<ApiProcess> StartAsync(
        string connectionString,
        int port,
        CancellationToken cancellationToken = default)
    {
        string apiDll = RepositoryLayout.ResolveProjectBinary("MinimalBankSystem.Api");
        string apiDirectory = Path.GetDirectoryName(apiDll)!;

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            ArgumentList = { apiDll },
            WorkingDirectory = apiDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment["ConnectionStrings__Database"] = connectionString;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the API process.");

        ApiProcess apiProcess = new(process, port);
        await apiProcess.WaitUntilListeningAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return apiProcess;
    }

    public async Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetAsync($"http://127.0.0.1:{Port}{path}", cancellationToken);
    }

    public async Task WaitUntilListeningAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        while (true)
        {
            timeoutSource.Token.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The API process exited before it started listening. " +
                    $"Exit code {process.ExitCode}. Stderr: {await standardError}");
            }

            try
            {
                using TcpClient client = new();
                await client.ConnectAsync(IPAddress.Loopback, Port, timeoutSource.Token);
                return;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"The API process did not start listening on port {Port} within {timeout}.");
            }
            catch (SocketException)
            {
                await Task.Delay(250, timeoutSource.Token);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stopped)
        {
            return;
        }

        stopped = true;

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        await process.WaitForExitAsync();
    }
}
