namespace MinimalBankSystem.IntegrationTests;

internal static class TestExecutionCollections
{
    public const string ConsoleSensitive = "Console-sensitive integration tests";

    public const string DockerCleanupFailureInjection = "Docker cleanup failure injection";
}

[CollectionDefinition(TestExecutionCollections.ConsoleSensitive, DisableParallelization = true)]
public sealed class ConsoleSensitiveTestGroup;

[CollectionDefinition(TestExecutionCollections.DockerCleanupFailureInjection, DisableParallelization = true)]
public sealed class DockerCleanupFailureInjectionTestGroup;
