using System.Xml;
using System.Xml.Linq;

namespace MinimalBankSystem.Tests;

public sealed class SolutionConfigurationTests
{
    [Fact]
    public void ProjectsInheritNet10AndFollowTheApprovedDependencyDirection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var commonProperties = LoadXml(Path.Combine(repositoryRoot.FullName, "Directory.Build.props"));

        Assert.Equal("net10.0", GetSingleProperty(commonProperties, "TargetFramework"));
        Assert.Equal("enable", GetSingleProperty(commonProperties, "Nullable"));
        Assert.Equal("true", GetSingleProperty(commonProperties, "TreatWarningsAsErrors"));

        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj"] =
            [
                "src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj",
            ],
            ["src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj"] =
            [
                "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj",
            ],
            ["src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj"] = [],
            ["src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj"] =
            [
                "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj",
            ],
            ["tests/MinimalBankSystem.Tests/MinimalBankSystem.Tests.csproj"] =
            [
                "src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj",
                "src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj",
                "src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj",
                "src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj",
            ],
        };

        foreach (var (projectPath, expectedProjectReferences) in expectedReferences)
        {
            var absoluteProjectPath = Path.Combine(repositoryRoot.FullName, projectPath);
            var project = LoadXml(absoluteProjectPath);

            Assert.Empty(project.Descendants("TargetFramework"));

            var actualProjectReferences = project
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(reference => reference is not null)
                .Select(reference => Path.GetFullPath(reference!, Path.GetDirectoryName(absoluteProjectPath)!))
                .Select(reference => Path.GetRelativePath(repositoryRoot.FullName, reference).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedProjectReferences.Order(StringComparer.Ordinal), actualProjectReferences);
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MinimalBankSystem.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static XDocument LoadXml(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    private static string GetSingleProperty(XDocument document, string propertyName)
    {
        return Assert.Single(document.Descendants(propertyName)).Value;
    }
}
