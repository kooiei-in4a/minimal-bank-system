using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Migrator;

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
    public void ProjectReferencesFollowAdr0001AndFnd04Ownership()
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

        Assert.Equal("10.0.10", ReadPackageVersion(packages, "Microsoft.EntityFrameworkCore"));
        Assert.Equal("10.0.10", ReadPackageVersion(packages, "Microsoft.EntityFrameworkCore.Design"));
        Assert.Equal("10.0.10", ReadPackageVersion(packages, "Microsoft.EntityFrameworkCore.Relational"));
        Assert.Equal("10.0.3", ReadPackageVersion(packages, "Npgsql"));
        Assert.Equal("10.0.3", ReadPackageVersion(packages, "Npgsql.EntityFrameworkCore.PostgreSQL"));
    }

    [Fact]
    public void RepositoryLocalDotnetEfToolIsPinnedTo101010()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot.FullName, ".config", "dotnet-tools.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        JsonElement dotnetEf = document.RootElement.GetProperty("tools").GetProperty("dotnet-ef");
        Assert.Equal("10.0.10", dotnetEf.GetProperty("version").GetString());
        Assert.False(dotnetEf.GetProperty("rollForward").GetBoolean());
    }

    [Fact]
    public void MigrationBudgetIsFixedAtSixtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), BankPersistence.MigrationTimeout);
        Assert.Equal(60, BankPersistence.MigrationCommandTimeoutSeconds);
    }

    [Fact]
    public void RequireConnectionStringRejectsMissingConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BankPersistence.RequireConnectionString(configuration));

        Assert.Contains("ConnectionStrings:Database", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SQLite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigratorReturnsNonZeroWhenConnectionStringIsMissing()
    {
        string? previous = Environment.GetEnvironmentVariable(
            BankPersistence.ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                BankPersistence.ConnectionStringEnvironmentVariable,
                null);

            int exitCode = await MigrationEntryPoint.RunAsync([]);
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                BankPersistence.ConnectionStringEnvironmentVariable,
                previous);
        }
    }

    [Fact]
    public void InfrastructureAssemblyExposesInitialFoundationMigration()
    {
        Type[] migrationTypes = typeof(BankDbContext).Assembly
            .GetTypes()
            .Where(type => typeof(Microsoft.EntityFrameworkCore.Migrations.Migration).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .ToArray();

        Assert.Contains(migrationTypes, type => type.Name == "InitialFoundation");

        DbContextOptionsBuilder<BankDbContext> optionsBuilder = new();
        BankPersistence.ConfigureNpgsql(
            optionsBuilder,
            "Host=127.0.0.1;Port=5432;Database=mbs_migration_probe;Username=postgres;Password=unused");

        using BankDbContext dbContext = new(optionsBuilder.Options);
        string[] migrations = dbContext.Database.GetMigrations().ToArray();

        Assert.Single(migrations);
        Assert.EndsWith("_InitialFoundation", migrations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ApiProgramDoesNotAutoMigrateOrEnsureCreated()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string programPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Api",
            "Program.cs");
        string source = File.ReadAllText(programPath);

        Assert.DoesNotContain("Migrate(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MigrateAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureCreated(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureCreatedAsync(", source, StringComparison.Ordinal);
        Assert.Contains("AddBankPersistence", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InitialFoundationMigrationContainsNoBusinessDdl()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string migrationsDirectory = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "MinimalBankSystem.Infrastructure",
            "Persistence",
            "Migrations");

        string migrationPath = Directory
            .EnumerateFiles(migrationsDirectory, "*_InitialFoundation.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));

        string source = File.ReadAllText(migrationPath);
        Assert.Contains("partial class InitialFoundation", source, StringComparison.Ordinal);

        string[] forbidden =
        [
            "CreateTable",
            "CreateSequence",
            "CreateIndex",
            "AddForeignKey",
            "Customer",
            "Account",
            "Operator",
            "Identity",
            "AuditLog",
            "Transaction",
            "Idempotency",
        ];

        foreach (string token in forbidden)
        {
            Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DotnetEfToolReportsPinnedVersion()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = "tool run dotnet-ef -- --version",
            WorkingDirectory = repositoryRoot.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet tool run.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, stderr);
        string[] lines = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("10.0.10", lines, StringComparer.Ordinal);
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

    private static string ReadPackageVersion(XDocument packages, string packageId)
    {
        XElement package = Assert.Single(
            packages.Descendants("PackageVersion"),
            element => element.Attribute("Include")?.Value == packageId);
        return package.Attribute("Version")?.Value
            ?? throw new InvalidOperationException($"Package '{packageId}' is missing a Version attribute.");
    }
}
