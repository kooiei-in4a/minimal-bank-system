namespace MinimalBankSystem.IntegrationTests.Compose;

/// <summary>Locked FND-05 Compose contracts visible to production tests (D-02..D-05).</summary>
internal static class ComposeContracts
{
    public const string CanonicalProjectName = "minimal-bank-system-fnd05";

    public const string ComposeFileName = "compose.yaml";

    public const string DatabasePasswordEnvironmentVariable = "MBS_DATABASE_PASSWORD";

    public const string PostgresServiceName = "postgres";

    public const string MigratorServiceName = "migrator";

    public const string ApiServiceName = "api";

    public const string NamedVolumeLogicalName = "postgres_data";

    public const string PostgresImageReference =
        "postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";

    public const string DotnetSdkImageReference =
        "mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0";

    public const string DotnetAspNetImageReference =
        "mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b";

    public const string PostgresImageDigest =
        "sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";

    public const string ExpectedMigrationIdSuffix = "_InitialFoundation";

    public const string SecretSentinel = "FND05_COMPOSE_SECRET_SENTINEL_7E4B91A0";

    public const string PathMarkerMigratorFailed = "Migration failed. The deployment must not continue.";

    public const string PathMarkerMigratorCompleted = "Migration completed. Applied migration history:";

    public const string PathMarkerMigratorApplying = "pending migration(s) with a";
}
