using Xunit;

namespace MinimalBankSystem.IntegrationTests;

[CollectionDefinition(ConsoleCapture, DisableParallelization = true)]
public sealed class ConsoleCaptureTestGroup
{
    public const string ConsoleCapture = "Console capture";
}

[CollectionDefinition(PostgreSqlIntegration, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationTestGroup : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string PostgreSqlIntegration = "PostgreSQL integration";
}

public static class TestExecutionCollections
{
    public const string ConsoleCapture = ConsoleCaptureTestGroup.ConsoleCapture;
    public const string PostgreSqlIntegration = PostgreSqlIntegrationTestGroup.PostgreSqlIntegration;
}
