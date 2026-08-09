using Docker.DotNet;
using Docker.DotNet.Models;
using Testcontainers.PostgreSql;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal interface IPostgreSqlContainerDisposer
{
    ValueTask DisposeAsync(PostgreSqlContainer container);
}

internal sealed class TestcontainersPostgreSqlContainerDisposer : IPostgreSqlContainerDisposer
{
    public ValueTask DisposeAsync(PostgreSqlContainer container) => container.DisposeAsync();
}

internal interface IContainerResourceOwnerFactory
{
    IContainerResourceOwner Create(string ownershipLabel, string ownershipId);
}

internal interface IContainerResourceOwner : IDisposable
{
    Task<IReadOnlyList<string>> GetContainerIdsAsync(
        CancellationToken cancellationToken = default);

    Task RemoveContainersAsync(CancellationToken cancellationToken = default);
}

internal sealed class DockerContainerResourceOwnerFactory : IContainerResourceOwnerFactory
{
    public IContainerResourceOwner Create(string ownershipLabel, string ownershipId) =>
        new DockerContainerResourceOwner(
            new DockerClientBuilder().Build(),
            ownershipLabel,
            ownershipId);
}

internal sealed class DockerContainerResourceOwner : IContainerResourceOwner
{
    private readonly DockerClient client;
    private readonly string labelSelector;

    public DockerContainerResourceOwner(
        DockerClient client,
        string ownershipLabel,
        string ownershipId)
    {
        this.client = client;
        labelSelector = $"{ownershipLabel}={ownershipId}";
    }

    public async Task<IReadOnlyList<string>> GetContainerIdsAsync(
        CancellationToken cancellationToken = default)
    {
        ContainersListParameters parameters = new()
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>(StringComparer.Ordinal)
            {
                ["label"] = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [labelSelector] = true,
                },
            },
        };

        IList<ContainerListResponse> containers =
            await client.Containers.ListContainersAsync(parameters, cancellationToken);

        return containers.Select(candidate => candidate.ID).ToArray();
    }

    public async Task RemoveContainersAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> containerIds = await GetContainerIdsAsync(cancellationToken);

        foreach (string containerId in containerIds)
        {
            try
            {
                await client.Containers.RemoveContainerAsync(
                    containerId,
                    new ContainerRemoveParameters
                    {
                        Force = true,
                        RemoveVolumes = true,
                    },
                    cancellationToken);
            }
            catch (DockerContainerNotFoundException)
            {
                // The desired final state is already established.
            }
        }

        IReadOnlyList<string> remainingContainerIds = await GetContainerIdsAsync(cancellationToken);

        if (remainingContainerIds.Count != 0)
        {
            throw new InvalidOperationException(
                $"Docker still reports fixture-owned container(s): {string.Join(", ", remainingContainerIds)}.");
        }
    }

    public void Dispose() => client.Dispose();
}

internal static class DockerContainerResourceProbe
{
    public static async Task<bool> ExistsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        using DockerClient client = new DockerClientBuilder().Build();

        try
        {
            _ = await client.Containers.InspectContainerAsync(containerId, cancellationToken);
            return true;
        }
        catch (DockerContainerNotFoundException)
        {
            return false;
        }
    }
}
