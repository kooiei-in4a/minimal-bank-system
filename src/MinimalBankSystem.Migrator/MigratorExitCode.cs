namespace MinimalBankSystem.Migrator;

/// <summary>Process exit codes for the one-shot migration command.</summary>
public static class MigratorExitCode
{
    /// <summary>Every pending migration was applied.</summary>
    public const int Success = 0;

    /// <summary>Configuration, connection, authentication or migration failure.</summary>
    public const int Failure = 1;

    /// <summary>The fixed migration budget elapsed.</summary>
    public const int Timeout = 2;
}
