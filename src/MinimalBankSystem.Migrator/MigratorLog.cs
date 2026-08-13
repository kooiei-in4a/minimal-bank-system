using Microsoft.Extensions.Logging;

namespace MinimalBankSystem.Migrator;

internal static partial class MigratorLog
{
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
        Message = "Migration exceeded the fixed {TimeoutSeconds}s budget and was cancelled. The deployment must not continue.")]
    public static partial void MigrationTimedOut(ILogger logger, int timeoutSeconds);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Migration failed. The deployment must not continue.")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Enforced read-only migration-history privilege boundary for API runtime role {ApiRuntimeRoleName}.")]
    public static partial void PrivilegeBoundaryEnforced(ILogger logger, string apiRuntimeRoleName);
}
