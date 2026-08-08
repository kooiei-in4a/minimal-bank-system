using System.Text.RegularExpressions;
using System.Xml.Linq;
using MinimalBankSystem.PostgresIntegrationTests.Fixtures;

namespace MinimalBankSystem.PostgresIntegrationTests;

/// <summary>
/// Guards the policy decisions this fixture is required to keep. These checks read repository
/// files and need no container, so they also run when the fixture itself is unavailable.
/// </summary>
public sealed partial class PostgresTestPolicyTests
{
    [Fact]
    public void ContinuousIntegrationPinsTheSameImageDigest()
    {
        string workflow = File.ReadAllText(
            Path.Combine(FindRepositoryRoot().FullName, ".github", "workflows", "build-test.yml"));

        Assert.Contains(PostgresTestImage.Reference, workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void NoInMemoryOrSqliteProviderIsAvailableToTests()
    {
        XDocument packages = XDocument.Load(
            Path.Combine(FindRepositoryRoot().FullName, "Directory.Packages.props"));

        string[] packageNames = packages
            .Descendants("PackageVersion")
            .Select(package => package.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            packageNames,
            name => name.Contains("InMemory", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoPostgresIntegrationTestCanBeSkipped()
    {
        DirectoryInfo testProject = new(
            Path.Combine(
                FindRepositoryRoot().FullName,
                "tests",
                "MinimalBankSystem.PostgresIntegrationTests"));

        string[] offenders = testProject
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Where(file => SkipUsage().IsMatch(File.ReadAllText(file.FullName)))
            .Select(file => file.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Matches the only way an xUnit v2 test can opt out of running. A container that will not
    /// start must fail the run, so this must never appear in this project.
    /// </summary>
    [GeneratedRegex(@"\bSkip\s*=")]
    private static partial Regex SkipUsage();

    private static bool IsBuildOutput(FileInfo file) =>
        file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
