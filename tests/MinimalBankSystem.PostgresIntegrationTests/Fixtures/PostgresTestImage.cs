namespace MinimalBankSystem.PostgresIntegrationTests.Fixtures;

/// <summary>
/// The single pinned PostgreSQL container image used by every PostgreSQL integration test.
/// </summary>
/// <remarks>
/// The reference is pinned by digest so that a repository, CI run or developer machine can never
/// silently move to another PostgreSQL build. Nothing in this assembly may build an image
/// reference from anything other than <see cref="Reference"/>.
/// </remarks>
public static class PostgresTestImage
{
    /// <summary>The digest of the pinned image.</summary>
    public const string Digest =
        "sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    /// <summary>The fully pinned image reference, including both tag and digest.</summary>
    public const string Reference = "postgres:18.4@" + Digest;

    /// <summary>The prefix that <c>SELECT version()</c> must report for the pinned image.</summary>
    public const string ExpectedVersionPrefix = "PostgreSQL 18.";

    /// <summary>The lowest <c>server_version_num</c> accepted for the pinned image.</summary>
    public const int MinimumServerVersionNumber = 180000;
}
