using System.Net;
using System.Net.Sockets;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// A loopback TCP endpoint that forwards Docker Engine traffic to the real Docker endpoint and can
/// stop forwarding on demand.
/// </summary>
/// <remarks>
/// Container cleanup failures have to be injected at the Docker transport, because a fixture that
/// cancels before Docker is contacted never exercises a real removal failure. Creating the
/// container through this proxy and then breaking it leaves a genuinely running container that
/// Testcontainers cannot delete.
/// </remarks>
internal sealed class DockerEndpointFaultProxy : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource shutdown = new();
    private readonly List<Stream> liveConnections = [];
    private readonly Lock connectionGate = new();
    private readonly Task acceptLoop;
    private volatile bool forwarding = true;

    internal DockerEndpointFaultProxy()
    {
        UpstreamEndpoint = DockerEngineEndpoint.Resolve(null);
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Endpoint = $"tcp://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>
    /// Gets the Docker endpoint clients should be pointed at.
    /// </summary>
    internal string Endpoint { get; }

    /// <summary>
    /// Gets the real Docker endpoint, used by assertions that must observe actual daemon state.
    /// </summary>
    internal Uri UpstreamEndpoint { get; }

    /// <summary>
    /// Stops forwarding and tears down every connection already in flight.
    /// </summary>
    internal void BreakDockerAccess()
    {
        forwarding = false;

        // Pooled connections must die too, otherwise an in-flight request could still be answered.
        Stream[] established;

        lock (connectionGate)
        {
            established = [.. liveConnections];
            liveConnections.Clear();
        }

        foreach (Stream connection in established)
        {
            Close(connection);
        }
    }

    /// <summary>
    /// Resumes forwarding so a retried cleanup can reach Docker again.
    /// </summary>
    internal void RestoreDockerAccess() => forwarding = true;

    public async ValueTask DisposeAsync()
    {
        await shutdown.CancelAsync();
        listener.Stop();
        BreakDockerAccess();

        try
        {
            await acceptLoop;
        }
        catch (OperationCanceledException)
        {
            // Expected: the accept loop ends with the proxy.
        }

        shutdown.Dispose();
    }

    private static void Close(Stream connection)
    {
        try
        {
            connection.Dispose();
        }
        catch (Exception)
        {
            // Tearing a connection down is best effort; it is already being abandoned.
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            Socket client;

            try
            {
                client = await listener.AcceptSocketAsync(shutdown.Token);
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or SocketException
                or ObjectDisposedException)
            {
                return;
            }

            if (!forwarding)
            {
                client.Dispose();
                continue;
            }

            _ = Task.Run(() => ForwardAsync(client));
        }
    }

    private async Task ForwardAsync(Socket client)
    {
        NetworkStream clientStream = new(client, ownsSocket: true);
        Stream? upstream = null;

        try
        {
            upstream = await DockerEngineEndpoint.ConnectAsync(UpstreamEndpoint, shutdown.Token);
            Track(clientStream, upstream);

            Task inbound = clientStream.CopyToAsync(upstream, shutdown.Token);
            Task outbound = upstream.CopyToAsync(clientStream, shutdown.Token);

            await Task.WhenAny(inbound, outbound);
        }
        catch (Exception)
        {
            // A forwarded connection that breaks is either the injected fault or shutdown.
        }
        finally
        {
            Untrack(clientStream, upstream);
            Close(clientStream);

            if (upstream is not null)
            {
                Close(upstream);
            }
        }
    }

    private void Track(Stream clientStream, Stream upstream)
    {
        lock (connectionGate)
        {
            liveConnections.Add(clientStream);
            liveConnections.Add(upstream);
        }
    }

    private void Untrack(Stream clientStream, Stream? upstream)
    {
        lock (connectionGate)
        {
            liveConnections.Remove(clientStream);

            if (upstream is not null)
            {
                liveConnections.Remove(upstream);
            }
        }
    }
}
