using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Operational health contract required by Accepted ADR-0008.
/// </summary>
/// <remarks>
/// Liveness reports process liveness only, so PostgreSQL can never influence it. Readiness
/// additionally requires PostgreSQL connectivity and the canonical FND-04 EF Core migration set
/// to be fully applied. Health responses are operational; they are not the FND-02 business error
/// envelope and never disclose a connection string, credential, exception detail or stack trace.
/// </remarks>
public static class HealthContract
{
    /// <summary>Process liveness endpoint.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Traffic readiness endpoint.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>Registration name of the single readiness dependency check.</summary>
    public const string ReadinessCheckName = "postgresql-readiness";

    /// <summary>Tag selecting the checks readiness runs and liveness must never run.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>The whole sanitized success body.</summary>
    public const string HealthyBody = "healthy";

    /// <summary>The whole sanitized failure body.</summary>
    public const string UnhealthyBody = "unhealthy";

    /// <summary>Content type of every health response.</summary>
    public const string ResponseContentType = "text/plain; charset=utf-8";

    // Fixed internal reasons. They reach technical logs only, never a health response.
    internal const string ReadyReason = "ready";
    internal const string DatabaseUnreachableReason = "database_unreachable";
    internal const string MigrationsPendingReason = "migrations_pending";
    internal const string DependencyFailureReason = "dependency_failure";

    /// <summary>Bounded readiness budget so a stalled dependency cannot hang the probe.</summary>
    internal static TimeSpan ReadinessTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Liveness selects no dependency check at all.</summary>
    internal static HealthCheckOptions Liveness { get; } = Create(_ => false);

    /// <summary>Readiness selects exactly the checks tagged as readiness dependencies.</summary>
    internal static HealthCheckOptions Readiness { get; } =
        Create(registration => registration.Tags.Contains(ReadinessTag));

    private static HealthCheckOptions Create(Func<HealthCheckRegistration, bool> predicate) =>
        new()
        {
            Predicate = predicate,
            AllowCachingResponses = false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
            ResponseWriter = WriteSanitizedStatusAsync,
        };

    // The whole response body is one of two fixed tokens. Check names, descriptions, durations,
    // exception data and dependency identities are deliberately not part of the contract.
    private static Task WriteSanitizedStatusAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = ResponseContentType;

        return context.Response.WriteAsync(
            report.Status == HealthStatus.Healthy ? HealthyBody : UnhealthyBody,
            context.RequestAborted);
    }
}

/// <summary>
/// Readiness dependency check: PostgreSQL must be reachable and the canonical migration set must
/// already be applied. It only reads existing EF Core migration metadata and never evolves schema.
/// </summary>
internal sealed class PostgreSqlReadinessHealthCheck(
    IServiceProvider services,
    ILogger<PostgreSqlReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Persistence is resolved inside the guarded region on purpose. A configuration or
            // provider construction failure must surface as an operational readiness failure and
            // must never escape into the FND-02 business error envelope.
            BankDbContext dbContext = services.GetRequiredService<BankDbContext>();

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return NotReady(HealthContract.DatabaseUnreachableReason);
            }

            // The FND-04 migration history is the only readiness authority. No marker table,
            // no business schema probe, no schema evolution.
            IEnumerable<string> pendingMigrations =
                await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

            return pendingMigrations.Any()
                ? NotReady(HealthContract.MigrationsPendingReason)
                : HealthCheckResult.Healthy(HealthContract.ReadyReason);
        }
        catch (Exception exception)
        {
            HealthLog.ReadinessDependencyFailed(
                logger,
                HealthContract.DependencyFailureReason,
                exception.GetType().FullName ?? exception.GetType().Name);

            return Unhealthy(HealthContract.DependencyFailureReason);
        }
    }

    private HealthCheckResult NotReady(string reason)
    {
        HealthLog.ReadinessRejected(logger, reason);

        return Unhealthy(reason);
    }

    // No exception is attached, so no framework consumer can render exception detail.
    private static HealthCheckResult Unhealthy(string reason) =>
        new(HealthStatus.Unhealthy, reason);
}

internal static partial class HealthLog
{
    // Allow-list only. Never pass an exception object, message, stack trace, connection string,
    // credential, token or personal data.
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Error,
        Message = "Readiness rejected with {ReadinessReason}.")]
    public static partial void ReadinessRejected(ILogger logger, string readinessReason);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "Readiness rejected with {ReadinessReason}. Exception type: {ExceptionType}.")]
    public static partial void ReadinessDependencyFailed(
        ILogger logger,
        string readinessReason,
        string exceptionType);
}
