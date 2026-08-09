using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Migrator;

/// <summary>
/// Source-generated migrator log messages.
/// </summary>
internal static partial class MigratorLog
{
    /// <summary>Logger category used for migrator output.</summary>
    public const string Category = "MinimalBankSystem.Migrator";

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Applying {PendingMigrationCount} pending migration(s) with a {TimeoutSeconds}s budget: {PendingMigrations}")]
    public static partial void ApplyingMigrations(
        ILogger logger,
        int pendingMigrationCount,
        int timeoutSeconds,
        string pendingMigrations);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Migration completed. Applied migration history: {AppliedMigrations}")]
    public static partial void MigrationCompleted(ILogger logger, string appliedMigrations);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Migration exceeded the fixed {TimeoutSeconds}s budget and was cancelled. The database may be partially migrated and the deployment must not continue.")]
    public static partial void MigrationTimedOut(ILogger logger, int timeoutSeconds);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Migration failed. The deployment must not continue.")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);
}
