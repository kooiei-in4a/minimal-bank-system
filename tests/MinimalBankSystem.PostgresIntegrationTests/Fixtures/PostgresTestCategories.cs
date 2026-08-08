namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// The trait used to select the real PostgreSQL integration tests from the command line.
/// </summary>
public static class PostgresTestCategories
{
    /// <summary>The trait name.</summary>
    public const string Category = "Category";

    /// <summary>
    /// The trait value shared by every test that requires the real PostgreSQL container.
    /// Select with <c>dotnet test --filter "Category=PostgresIntegration"</c>.
    /// </summary>
    public const string PostgresIntegration = "PostgresIntegration";
}
