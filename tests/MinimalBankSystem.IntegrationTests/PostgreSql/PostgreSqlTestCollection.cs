using Xunit;

namespace MinimalBankSystem.IntegrationTests.PostgreSql;

public static class PostgreSqlTestCollections
{
    public const string Name = "PostgreSql";

    public const string CategoryTraitName = "Category";

    public const string CategoryTraitValue = "PostgreSql";
}

[CollectionDefinition(PostgreSqlTestCollections.Name, DisableParallelization = true)]
public sealed class PostgreSqlTestCollectionDefinition : ICollectionFixture<PostgreSqlContainerFixture>;
