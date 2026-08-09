using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Minimal Docker Engine API access that does not go through Testcontainers.
/// </summary>
/// <remarks>
/// Testcontainers latches the disposed state of a container instance before it removes the
/// container, so a container instance whose removal failed can never remove that container again.
/// The surviving resource identity is the Docker container id, and this type is what turns that id
/// back into a real removal.
/// </remarks>
internal static class DockerEngineEndpoint
{
    private const string DefaultWindowsPipeName = "docker_engine";
    private const string DefaultUnixSocketPath = "/var/run/docker.sock";
    private const string WindowsPipePathPrefix = "pipe/";

    /// <summary>
    /// Resolves the Docker endpoint the fixture and Testcontainers both talk to.
    /// </summary>
    /// <param name="configuredEndpoint">An explicitly configured endpoint, or <see langword="null" />.</param>
    /// <returns>The Docker endpoint URI.</returns>
    internal static Uri Resolve(string? configuredEndpoint)
    {
        if (configuredEndpoint is not null)
        {
            return new Uri(configuredEndpoint);
        }

        string? dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return new Uri(dockerHost);
        }

        return OperatingSystem.IsWindows()
            ? new Uri($"npipe://./{WindowsPipePathPrefix}{DefaultWindowsPipeName}")
            : new Uri($"unix://{DefaultUnixSocketPath}");
    }

    /// <summary>
    /// Removes a container by its Docker id, independently of any Testcontainers instance.
    /// </summary>
    /// <param name="endpoint">The Docker endpoint.</param>
    /// <param name="containerId">The Docker container id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the daemon no longer knows the container.</returns>
    internal static async Task RemoveContainerAsync(
        Uri endpoint,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        int statusCode = await SendAsync(
            endpoint,
            "DELETE",
            $"/containers/{containerId}?force=true&v=true",
            cancellationToken);

        // 204: removed by this call. 404: this daemon no longer knows the id. Releasing ownership
        // on 404 assumes the resolved endpoint is the endpoint the container was created through,
        // which holds for an explicit endpoint, DOCKER_HOST, and the platform default socket.
        if (statusCode is not (204 or 404))
        {
            throw new InvalidOperationException(
                $"Docker refused to remove container '{containerId}' through '{endpoint}' " +
                $"and answered HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    /// <summary>
    /// Asks the Docker daemon whether a container still exists.
    /// </summary>
    /// <param name="endpoint">The Docker endpoint.</param>
    /// <param name="containerId">The Docker container id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the daemon still knows the container; otherwise, false.</returns>
    internal static async Task<bool> ContainerExistsAsync(
        Uri endpoint,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        int statusCode = await SendAsync(
            endpoint,
            "GET",
            $"/containers/{containerId}/json",
            cancellationToken);

        return statusCode switch
        {
            200 => true,
            404 => false,
            _ => throw new InvalidOperationException(
                $"Docker could not report the state of container '{containerId}' through '{endpoint}' " +
                $"and answered HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}."),
        };
    }

    /// <summary>
    /// Opens a byte stream to the Docker endpoint.
    /// </summary>
    /// <param name="endpoint">The Docker endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected stream.</returns>
    internal static async Task<Stream> ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        switch (endpoint.Scheme)
        {
            case "npipe":
            {
                NamedPipeClientStream pipe = new(
                    endpoint.Host.Length == 0 ? "." : endpoint.Host,
                    ReadPipeName(endpoint),
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(cancellationToken);
                return pipe;
            }

            case "unix":
            {
                Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(ReadSocketPath(endpoint)), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }

            case "tcp":
            case "http":
            {
                Socket socket = new(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }

            default:
                throw new InvalidOperationException(
                    $"Docker endpoint '{endpoint}' uses the unsupported scheme '{endpoint.Scheme}'.");
        }
    }

    private static string ReadPipeName(Uri endpoint)
    {
        string path = endpoint.AbsolutePath.Trim('/');

        return path.StartsWith(WindowsPipePathPrefix, StringComparison.Ordinal)
            ? path[WindowsPipePathPrefix.Length..]
            : path;
    }

    private static string ReadSocketPath(Uri endpoint)
    {
        // Both 'unix:///var/run/docker.sock' and 'unix:/var/run/docker.sock' are in use.
        string path = endpoint.AbsolutePath;

        return path.Length == 0 || path == "/" ? DefaultUnixSocketPath : path;
    }

    private static async Task<int> SendAsync(
        Uri endpoint,
        string method,
        string requestPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = await ConnectAsync(endpoint, cancellationToken);

            string request =
                $"{method} {requestPath} HTTP/1.1\r\n" +
                "Host: docker\r\n" +
                "Accept: application/json\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);
            await stream.FlushAsync(cancellationToken);

            using StreamReader reader = new(stream, Encoding.ASCII, false, 256, leaveOpen: true);
            string? statusLine = await reader.ReadLineAsync(cancellationToken);

            if (statusLine is null)
            {
                throw new InvalidOperationException(
                    $"The Docker endpoint '{endpoint}' closed the connection without answering " +
                    $"'{method} {requestPath}'.");
            }

            string[] statusLineParts = statusLine.Split(' ', 3);

            if (statusLineParts.Length < 2 ||
                !int.TryParse(
                    statusLineParts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int statusCode))
            {
                throw new InvalidOperationException(
                    $"The Docker endpoint '{endpoint}' answered '{method} {requestPath}' with the " +
                    $"unreadable status line '{statusLine}'.");
            }

            return statusCode;
        }
        catch (Exception exception)
            when (exception is not InvalidOperationException and not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"The Docker request '{method} {requestPath}' to '{endpoint}' failed.",
                exception);
        }
    }
}
