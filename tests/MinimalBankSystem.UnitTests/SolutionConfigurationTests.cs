using System.Xml.Linq;
using System.Text.Json;

namespace MinimalBankSystem.UnitTests;

public sealed class SolutionConfigurationTests
{
    private static readonly string[] ProjectPaths =
    [
        "src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj",
        "src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj",
        "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj",
        "src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj",
        "src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj",
        "tests/MinimalBankSystem.UnitTests/MinimalBankSystem.UnitTests.csproj",
        "tests/MinimalBankSystem.IntegrationTests/MinimalBankSystem.IntegrationTests.csproj",
    ];

    [Fact]
    public void AllProjectsInheritSharedNet10AndNullablePolicy()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        XDocument properties = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Build.props"));

        Assert.Equal("net10.0", ReadSingleProperty(properties, "TargetFramework"));
        Assert.Equal("enable", ReadSingleProperty(properties, "Nullable"));
        Assert.Equal("true", ReadSingleProperty(properties, "EnableNETAnalyzers"));
        Assert.Equal("latest-recommended", ReadSingleProperty(properties, "AnalysisLevel"));
        Assert.Equal("true", ReadSingleProperty(properties, "TreatWarningsAsErrors"));
        Assert.Equal("true", ReadSingleProperty(properties, "EnforceCodeStyleInBuild"));

        foreach (string projectPath in ProjectPaths)
        {
            XDocument project = XDocument.Load(Path.Combine(repositoryRoot.FullName, projectPath));
            Assert.Empty(project.Descendants("TargetFramework"));
            Assert.Empty(project.Descendants("Nullable"));
        }
    }

    [Fact]
    public void ProjectReferencesFollowAdr0001()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();

        AssertProjectReferences(
            repositoryRoot,
            "src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj",
            "src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj",
            "src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj");

        AssertProjectReferences(
            repositoryRoot,
            "src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj",
            "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj");

        AssertProjectReferences(
            repositoryRoot,
            "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj");

        AssertProjectReferences(
            repositoryRoot,
            "src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj",
            "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj");

        AssertProjectReferences(
            repositoryRoot,
            "src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj",
            "src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj");
    }

    [Fact]
    public void NuGetVersionsAreCentrallyPinnedExactly()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        XDocument packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        Assert.Equal("true", ReadSingleProperty(packages, "ManagePackageVersionsCentrally"));

        XElement[] packageVersions = packages.Descendants("PackageVersion").ToArray();
        Assert.NotEmpty(packageVersions);

        Assert.All(
            packageVersions,
            package =>
            {
                string? version = package.Attribute("Version")?.Value;
                Assert.False(string.IsNullOrWhiteSpace(version));
                Assert.True(Version.TryParse(version, out _), $"Package version '{version}' is not exact.");
            });
    }

    [Fact]
    public void Fnd04EfCoreAndNpgsqlVersionsArePinnedExactly()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        XDocument packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));
        Dictionary<string, string?> versions = packages
            .Descendants("PackageVersion")
            .ToDictionary(
                package => package.Attribute("Include")?.Value
                    ?? throw new InvalidOperationException("PackageVersion must have Include."),
                package => package.Attribute("Version")?.Value,
                StringComparer.Ordinal);

        Assert.Equal("10.0.10", versions["Microsoft.EntityFrameworkCore"]);
        Assert.Equal("10.0.10", versions["Microsoft.EntityFrameworkCore.Design"]);
        Assert.Equal("10.0.10", versions["Microsoft.EntityFrameworkCore.Relational"]);
        Assert.Equal("10.0.3", versions["Npgsql"]);
        Assert.Equal("10.0.3", versions["Npgsql.EntityFrameworkCore.PostgreSQL"]);
    }

    [Fact]
    public void Fnd04DotnetEfToolIsPinnedInTheRepositoryLocalManifest()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot.FullName, ".config", "dotnet-tools.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        string version = manifest.RootElement
            .GetProperty("tools")
            .GetProperty("dotnet-ef")
            .GetProperty("version")
            .GetString()
            ?? throw new InvalidOperationException("dotnet-ef tool version must be present.");

        Assert.Equal("10.0.10", version);
    }

    private static void AssertProjectReferences(
        DirectoryInfo repositoryRoot,
        string projectPath,
        params string[] expectedReferences)
    {
        string absoluteProjectPath = Path.Combine(repositoryRoot.FullName, projectPath);
        XDocument project = XDocument.Load(absoluteProjectPath);

        string[] actualReferences = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(reference => reference is not null)
            .Select(reference => Path.GetFullPath(reference!, Path.GetDirectoryName(absoluteProjectPath)!))
            .Select(reference => Path.GetRelativePath(repositoryRoot.FullName, reference).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.Order(StringComparer.Ordinal), actualReferences);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string ReadSingleProperty(XDocument document, string propertyName)
    {
        return Assert.Single(document.Descendants(propertyName)).Value;
    }
}
