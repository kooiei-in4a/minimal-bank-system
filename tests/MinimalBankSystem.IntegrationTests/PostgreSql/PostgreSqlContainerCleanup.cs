using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
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

internal sealed class DockerContainerResourceOwnerFactory(
    IDockerEndpointAuthenticationConfiguration? dockerEndpointAuthConfig)
    : IContainerResourceOwnerFactory
{
    public IContainerResourceOwner Create(string ownershipLabel, string ownershipId)
    {
        IDockerEndpointAuthenticationConfiguration resolvedDockerEndpoint =
            dockerEndpointAuthConfig ?? throw new InvalidOperationException(
                "Testcontainers could not resolve a Docker endpoint for independent container cleanup.");

        return new DockerContainerResourceOwner(
            resolvedDockerEndpoint,
            ownershipLabel,
            ownershipId);
    }
}

internal sealed class DockerContainerResourceOwner : IContainerResourceOwner
{
    private readonly IDockerEndpointAuthenticationConfiguration dockerEndpointAuthConfig;
    private readonly string labelSelector;

    public DockerContainerResourceOwner(
        IDockerEndpointAuthenticationConfiguration dockerEndpointAuthConfig,
        string ownershipLabel,
        string ownershipId)
    {
        this.dockerEndpointAuthConfig = dockerEndpointAuthConfig;
        labelSelector = $"{ownershipLabel}={ownershipId}";
    }

    public async Task<IReadOnlyList<string>> GetContainerIdsAsync(
        CancellationToken cancellationToken = default)
    {
        using DockerClient client = dockerEndpointAuthConfig.GetDockerClientBuilder().Build();
        return await GetContainerIdsAsync(client, cancellationToken);
    }

    public async Task RemoveContainersAsync(CancellationToken cancellationToken = default)
    {
        using DockerClient client = dockerEndpointAuthConfig.GetDockerClientBuilder().Build();
        IReadOnlyList<string> containerIds = await GetContainerIdsAsync(client, cancellationToken);
        List<Exception> cleanupFailures = [];

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
                // A concurrent remover already established the required state.
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }

        IReadOnlyList<string> remainingContainerIds;

        try
        {
            remainingContainerIds = await GetContainerIdsAsync(client, cancellationToken);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
            throw CreateCleanupException(cleanupFailures);
        }

        if (remainingContainerIds.Count != 0)
        {
            cleanupFailures.Add(new InvalidOperationException(
                $"Docker still reports fixture-owned container(s): {string.Join(", ", remainingContainerIds)}."));
        }

        if (cleanupFailures.Count != 0)
        {
            throw CreateCleanupException(cleanupFailures);
        }
    }

    public void Dispose()
    {
    }

    private async Task<IReadOnlyList<string>> GetContainerIdsAsync(
        DockerClient client,
        CancellationToken cancellationToken)
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

    private static InvalidOperationException CreateCleanupException(
        List<Exception> cleanupFailures)
    {
        Exception cause = cleanupFailures.Count == 1
            ? cleanupFailures.Single()
            : new AggregateException(cleanupFailures);

        return new InvalidOperationException(
            "Independent Docker cleanup could not verify that every fixture-owned container was removed.",
            cause);
    }
}

internal static class DockerContainerResourceProbe
{
    public static async Task<bool> ExistsAsync(
        string containerId,
        IDockerEndpointAuthenticationConfiguration? dockerEndpointAuthConfig = null,
        CancellationToken cancellationToken = default)
    {
        IDockerEndpointAuthenticationConfiguration resolvedDockerEndpoint =
            dockerEndpointAuthConfig ?? TestcontainersSettings.OS.DockerEndpointAuthConfig
            ?? throw new InvalidOperationException(
                "Testcontainers could not resolve a Docker endpoint for container verification.");

        using DockerClient client = resolvedDockerEndpoint.GetDockerClientBuilder().Build();

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
