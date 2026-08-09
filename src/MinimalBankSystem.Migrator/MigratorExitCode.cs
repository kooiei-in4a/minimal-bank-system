namespace MinimalBankSystem.Migrator;

/// <summary>
/// Process exit codes. Only <see cref="Success"/> means the database reached the latest migration.
/// </summary>
public static class MigratorExitCode
{
    /// <summary>Every pending migration was applied.</summary>
    public const int Success = 0;

    /// <summary>Configuration, connection, authentication or migration failure.</summary>
    public const int Failure = 1;

    /// <summary>The fixed migration budget elapsed before the migration completed.</summary>
    public const int Timeout = 2;
}
