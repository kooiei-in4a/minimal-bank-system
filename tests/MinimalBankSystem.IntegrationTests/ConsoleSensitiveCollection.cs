namespace MinimalBankSystem.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleSensitiveTestGroup
{
    public const string Name = "Console-sensitive integration tests";
}
