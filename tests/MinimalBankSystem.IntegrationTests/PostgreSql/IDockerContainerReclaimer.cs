namespace MinimalBankSystem.IntegrationTests.PostgreSql;

internal interface IDockerContainerReclaimer
{
    Task<bool> ExistsAsync(string containerId, CancellationToken cancellationToken = default);

    Task RemoveAsync(string containerId, CancellationToken cancellationToken = default);
}
