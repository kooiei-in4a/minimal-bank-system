using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// A minimal in-process Docker Engine API stub used to deterministically inject container
/// removal failures. It serves the requests Testcontainers 4.13.0 issues while starting a
/// PostgreSql container and fails exactly at the container removal call when
/// <see cref="FailContainerRemoval" /> is set.
/// </summary>
internal sealed class FakeDockerDaemon : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource shutdown = new();
    private readonly List<Task> handlers = new();
    private int execCounter;

    public FakeDockerDaemon()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Endpoint = $"tcp://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        MappedHostPort = AllocateFreePort();
        _ = Task.Run(AcceptLoopAsync);
    }

    public string Endpoint { get; }

    public string ContainerId { get; } = string.Concat(Enumerable.Repeat("1e", 32));

    /// <summary>
    /// Gets the host port reported as mapped to the container's PostgreSQL port. Nothing
    /// listens on it, so Npgsql connection attempts deterministically fail.
    /// </summary>
    public int MappedHostPort { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the container removal call fails with HTTP 500.
    /// </summary>
    public bool FailContainerRemoval { get; set; }

    /// <summary>
    /// Gets a value indicating whether the stub daemon still tracks the container resource.
    /// </summary>
    public bool ContainerExists { get; private set; } = true;

    /// <summary>
    /// Gets the number of container removal calls the stub daemon received.
    /// </summary>
    public int ContainerRemoveAttempts { get; private set; }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        listener.Stop();

        Task[] pending;
        lock (handlers)
        {
            pending = handlers.ToArray();
        }

        try
        {
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // The handlers run under per-connection timeouts; nothing else to await.
        }

        shutdown.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task AcceptLoopAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                return;
            }

            Task handler = Task.Run(() => HandleClientAsync(client));
            lock (handlers)
            {
                handlers.Add(handler);
            }

            _ = handler.ContinueWith(
                completed =>
                {
                    lock (handlers)
                    {
                        handlers.Remove(completed);
                    }
                },
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (CancellationTokenSource timeout =
               CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            CancellationToken cancellationToken = timeout.Token;

            (string method, string rawPath) = ReadRequest(stream, cancellationToken);
            string path = NormalizePath(rawPath);

            byte[] response = Route(method, path);
            await stream.WriteAsync(response, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }

    private byte[] Route(string method, string path)
    {
        if (path == "/_ping")
        {
            return Text(200, "OK");
        }

        if (path == "/version")
        {
            return Json(200, new
            {
                Version = "25.0.0",
                ApiVersion = "1.44",
                MinAPIVersion = "1.24",
                Os = "linux",
                Arch = "amd64",
            });
        }

        if (path == "/info")
        {
            return Json(200, new
            {
                ServerVersion = "25.0.0",
                OperatingSystem = "linux",
                MemTotal = 1L,
                Labels = Array.Empty<string>(),
            });
        }

        if (method == "GET" && path.StartsWith("/images/", StringComparison.Ordinal) &&
            path.EndsWith("/json", StringComparison.Ordinal))
        {
            return Json(200, new
            {
                Id = $"sha256:{new string('c', 64)}",
                RepoTags = RepoTag,
            });
        }

        if (method == "POST" && path == "/containers/create")
        {
            return Json(201, new
            {
                Id = ContainerId,
                Warnings = Array.Empty<string>(),
            });
        }

        if (method == "POST" && path == $"/containers/{ContainerId}/start")
        {
            return Raw(204);
        }

        if (method == "POST" && path == $"/containers/{ContainerId}/exec")
        {
            return Json(201, new { Id = $"exec-{Interlocked.Increment(ref execCounter)}" });
        }

        if (method == "POST" && path.StartsWith("/exec/", StringComparison.Ordinal) &&
            path.EndsWith("/start", StringComparison.Ordinal))
        {
            return HijackedFrames("localhost:5432 - accepting connections\n");
        }

        if (method == "GET" && path.StartsWith("/exec/", StringComparison.Ordinal) &&
            path.EndsWith("/json", StringComparison.Ordinal))
        {
            return Json(200, new { ExitCode = 0, Running = false });
        }

        if (method == "GET" && path == $"/containers/{ContainerId}/json")
        {
            if (!ContainerExists)
            {
                return Json(404, new { message = $"No such container: {ContainerId}" });
            }

            return Json(200, new
            {
                Id = ContainerId,
                Name = "/stub-postgres",
                State = new
                {
                    Status = "running",
                    Running = true,
                    StartedAt = "2026-08-09T00:00:00Z",
                    FinishedAt = "0001-01-01T00:00:00Z",
                },
                NetworkSettings = new
                {
                    Ports = new Dictionary<string, object[]>()
                    {
                        ["5432/tcp"] = new[]
                        {
                            new Dictionary<string, string>()
                            {
                                ["HostIp"] = "127.0.0.1",
                                ["HostPort"] = MappedHostPort.ToString(CultureInfo.InvariantCulture),
                            },
                        },
                    },
                },
            });
        }

        if (method == "GET" && path == $"/containers/{ContainerId}/logs")
        {
            return HijackedFrames("database system is ready to accept connections\n");
        }

        if (method == "DELETE" && path == $"/containers/{ContainerId}")
        {
            ContainerRemoveAttempts++;

            if (!ContainerExists)
            {
                return Json(404, new { message = $"No such container: {ContainerId}" });
            }

            if (FailContainerRemoval)
            {
                return Json(500, new { message = "injected container removal failure" });
            }

            ContainerExists = false;
            return Raw(204);
        }

        return Json(404, new { message = $"stub daemon does not know route '{path}'" });
    }

    private static (string Method, string Path) ReadRequest(NetworkStream stream, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new byte[1];

        while (builder.Length < 64 * 1024)
        {
            int count = stream.ReadAsync(buffer, 0, 1, cancellationToken).GetAwaiter().GetResult();

            if (count == 0)
            {
                throw new EndOfStreamException("The Docker API client closed the connection.");
            }

            builder.Append((char)buffer[0]);

            if (builder.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        string[] lines = builder.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] requestLine = lines[0].Split(' ');

        int bodyLength = 0;

        foreach (string line in lines.Skip(1))
        {
            int separator = line.IndexOf(':');

            if (separator > 0 &&
                line[..separator].Equals("content-length", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line[(separator + 1)..].Trim(), out int length))
            {
                bodyLength = length;
            }
        }

        int read = 0;
        var body = new byte[bodyLength];

        while (read < bodyLength)
        {
            int count = stream.ReadAsync(body, read, bodyLength - read, cancellationToken).GetAwaiter().GetResult();

            if (count == 0)
            {
                throw new EndOfStreamException("The Docker API client closed the connection.");
            }

            read += count;
        }

        return (requestLine[0], requestLine[1]);
    }

    private static string NormalizePath(string path)
    {
        int query = path.IndexOf('?');

        if (query >= 0)
        {
            path = path[..query];
        }

        if (path.Length > 3 && path[0] == '/' && path[1] == 'v' && char.IsAsciiDigit(path[2]))
        {
            int slash = path.IndexOf('/', 1);

            if (slash > 0)
            {
                return path[slash..];
            }
        }

        return path;
    }

    private static readonly string[] RepoTag = { "postgres:18.4" };

    private static int AllocateFreePort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static byte[] Text(int status, string body) =>
        Response(status, "text/plain", Encoding.UTF8.GetBytes(body));

    private static byte[] Json(int status, object body) =>
        Response(status, "application/json", Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(body)));

    private static byte[] Frames(int status, string stdout)
    {
        byte[] payload = Encoding.UTF8.GetBytes(stdout);
        var frames = new byte[8 + payload.Length];
        frames[0] = 1;
        frames[4] = (byte)(payload.Length >> 24);
        frames[5] = (byte)(payload.Length >> 16);
        frames[6] = (byte)(payload.Length >> 8);
        frames[7] = (byte)payload.Length;
        Array.Copy(payload, 0, frames, 8, payload.Length);
        return Response(status, "application/vnd.docker.raw-stream", frames);
    }

    private static byte[] HijackedFrames(string stdout)
    {
        byte[] payload = Encoding.UTF8.GetBytes(stdout);
        var frames = new byte[8 + payload.Length];
        frames[0] = 1;
        frames[4] = (byte)(payload.Length >> 24);
        frames[5] = (byte)(payload.Length >> 16);
        frames[6] = (byte)(payload.Length >> 8);
        frames[7] = (byte)payload.Length;
        Array.Copy(payload, 0, frames, 8, payload.Length);

        // Docker Engine hijacks the connection for exec and log streams: the response carries
        // no Content-Length or chunked framing, an Upgrade: tcp header, and the multiplexed
        // frames follow the headers. Without Upgrade: tcp the client treats a raw-stream
        // Content-Type as a chunked body, which is not hijackable.
        byte[] header = Encoding.UTF8.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Content-Type: application/vnd.docker.raw-stream\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: tcp\r\n\r\n");
        return header.Concat(frames).ToArray();
    }

    private static byte[] Raw(int status) =>
        Response(status, "application/json", Array.Empty<byte>());

    private static byte[] Response(int status, string contentType, byte[] body)
    {
        string reason = status switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "Error",
        };

        var builder = new StringBuilder();
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "HTTP/1.1 {0} {1}\r\nContent-Type: {2}\r\nContent-Length: {3}\r\nConnection: close\r\n\r\n",
            status,
            reason,
            contentType,
            body.Length);
        return Encoding.UTF8.GetBytes(builder.ToString()).Concat(body).ToArray();
    }
}
