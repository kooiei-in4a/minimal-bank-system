namespace MinimalBankSystem.IntegrationTests;

internal static class TestExecutionCollections
{
    public const string ConsoleSensitive = "Console-sensitive integration tests";

    public const string ComposeRuntime = "Compose-runtime integration tests";
}

[CollectionDefinition(TestExecutionCollections.ConsoleSensitive, DisableParallelization = true)]
public sealed class ConsoleSensitiveTestGroup;

[CollectionDefinition(TestExecutionCollections.ComposeRuntime, DisableParallelization = true)]
public sealed class ComposeRuntimeTestGroup;
