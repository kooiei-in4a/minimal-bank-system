using System.Text.Json;
using System.Text.RegularExpressions;
using MinimalBankSystem.IntegrationTests.Persistence;

namespace MinimalBankSystem.IntegrationTests.Compose;

/// <summary>
/// Static and config-level oracles for digest pinning, named volume, and secret render safety.
/// Detects M-05 / M-06 / M-04 (rendered config) protected contracts.
/// </summary>
[Trait("Category", "PostgreSqlIntegration")]
[Collection(TestExecutionCollections.ComposeRuntime)]
public sealed class ComposeStaticContractTests
{
    private static readonly Regex DigestQualifiedImage =
        new(@"@sha256:[a-f0-9]{64}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ComposeAndDockerfilesPinLockedImageDigests()
    {
        DirectoryInfo root = RepositoryLayout.RepositoryRoot;
        string compose = File.ReadAllText(Path.Combine(root.FullName, ComposeContracts.ComposeFileName));
        string apiDockerfile = File.ReadAllText(
            Path.Combine(root.FullName, "src", "MinimalBankSystem.Api", "Dockerfile"));
        string migratorDockerfile = File.ReadAllText(
            Path.Combine(root.FullName, "src", "MinimalBankSystem.Migrator", "Dockerfile"));

        Assert.Contains(ComposeContracts.PostgresImageReference, compose, StringComparison.Ordinal);
        Assert.Contains(ComposeContracts.DotnetSdkImageReference, apiDockerfile, StringComparison.Ordinal);
        Assert.Contains(ComposeContracts.DotnetAspNetImageReference, apiDockerfile, StringComparison.Ordinal);
        Assert.Contains(ComposeContracts.DotnetSdkImageReference, migratorDockerfile, StringComparison.Ordinal);
        Assert.Contains(ComposeContracts.DotnetAspNetImageReference, migratorDockerfile, StringComparison.Ordinal);

        Assert.DoesNotContain("postgres:18.4\n", compose.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("postgres:18.4\"", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("postgres:latest", compose, StringComparison.Ordinal);

        Assert.All(
            new[] { compose, apiDockerfile, migratorDockerfile },
            text => Assert.Matches(DigestQualifiedImage, text));
    }

    [Fact]
    public void ComposeDeclaresNamedPostgresVolumeNotAnonymousOrBind()
    {
        string compose = File.ReadAllText(
            Path.Combine(RepositoryLayout.RepositoryRoot.FullName, ComposeContracts.ComposeFileName));

        Assert.Contains("postgres_data:", compose, StringComparison.Ordinal);
        Assert.Contains("postgres_data:/var/lib/postgresql", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("./data:", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("/var/lib/postgresql/data", compose, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolvedComposeConfigKeepsDigestNamedVolumeAndHidesSecretSentinel()
    {
        await using ComposeProjectSession session = ComposeProjectSession.Create();
        await session.EnsureValidatedAsync();

        using JsonDocument config = await session.LoadConfigDocumentAsync();
        string configJson = config.RootElement.GetRawText();

        Assert.True(
            ComposeObservations.ConfigContainsDigestQualifiedPostgres(config.RootElement),
            "Resolved postgres image must remain digest-qualified (M-05 oracle).");
        Assert.True(
            ComposeObservations.ConfigUsesNamedPostgresVolume(config.RootElement),
            "Resolved postgres storage must use the named volume (M-06 oracle).");
        Assert.False(
            ComposeObservations.ConfigRendersSecretLiteral(configJson, session.Password),
            "Resolved compose config must not render the secret sentinel (M-04/D-03).");
        Assert.DoesNotContain(
            session.Password,
            configJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDoesNotCommitComposeSecretValues()
    {
        DirectoryInfo root = RepositoryLayout.RepositoryRoot;
        string[] scanned =
        [
            Path.Combine(root.FullName, ComposeContracts.ComposeFileName),
            Path.Combine(root.FullName, "deploy", "compose", "read-db-secret-and-exec.sh"),
            Path.Combine(root.FullName, "docs", "operations", "compose-runtime.md"),
        ];

        foreach (string path in scanned)
        {
            string text = File.ReadAllText(path);
            Assert.DoesNotContain(ComposeContracts.SecretSentinel, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Password=supersecret", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(File.Exists(Path.Combine(root.FullName, ".env")));
    }

    [Fact]
    public void SecretReaderDoesNotExpandPasswordOntoArgv()
    {
        string script = File.ReadAllText(
            Path.Combine(
                RepositoryLayout.RepositoryRoot.FullName,
                "deploy",
                "compose",
                "read-db-secret-and-exec.sh"));

        Assert.Contains("ConnectionStrings__Database=", script, StringComparison.Ordinal);
        Assert.Contains("exec \"$@\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$@\" \"$password", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--password", script, StringComparison.Ordinal);
    }
}
