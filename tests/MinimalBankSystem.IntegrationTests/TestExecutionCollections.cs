namespace MinimalBankSystem.IntegrationTests;

internal static class TestExecutionCollections
{
    public const string ConsoleSensitive = "Console-sensitive integration tests";
}

[CollectionDefinition(TestExecutionCollections.ConsoleSensitive, DisableParallelization = true)]
public sealed class ConsoleSensitiveTestGroup;
