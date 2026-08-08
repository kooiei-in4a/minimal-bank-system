namespace MinimalBankSystem.IntegrationTests.PostgreSql;

/// <summary>
/// Fixed PostgreSQL image reference for FND-03 provider integration tests.
/// Do not substitute floating tags such as <c>postgres:18</c> or <c>latest</c>.
/// </summary>
public static class PostgreSqlTestImage
{
    public const string DigestSha256 =
        "3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";

    public const string Reference =
        "postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a";
}
