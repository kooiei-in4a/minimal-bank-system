using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// The ADR-0008 operational health contract.
/// </summary>
public static class HealthContract
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadinessCheckName = "postgresql-readiness";
    public const string ReadinessTag = "ready";
    public const string HealthyBody = "healthy";
    public const string UnhealthyBody = "unhealthy";
    public const string ResponseContentType = "text/plain; charset=utf-8";

    internal static TimeSpan ReadinessTimeout { get; } = TimeSpan.FromSeconds(10);

    // The liveness predicate deliberately accepts no registrations. It therefore cannot resolve
    // a DbContext, connect to PostgreSQL, or execute a readiness-tagged check.
    internal static HealthCheckOptions Liveness { get; } = CreateOptions(_ => false);

    internal static HealthCheckOptions Readiness { get; } =
        CreateOptions(registration => registration.Tags.Contains(ReadinessTag));

    private static HealthCheckOptions CreateOptions(Func<HealthCheckRegistration, bool> predicate) =>
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
            ResponseWriter = WriteSanitizedResponseAsync,
        };

    private static Task WriteSanitizedResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = ResponseContentType;
        context.Response.Headers.CacheControl = "no-store, no-cache";

        string body = report.Status == HealthStatus.Healthy ? HealthyBody : UnhealthyBody;
        return context.Response.WriteAsync(body, context.RequestAborted);
    }
}

/// <summary>
/// Reports ready only when PostgreSQL is reachable and the canonical EF Core migration set has
/// been applied. This check is read-only: it never runs migrations or creates persistence state.
/// </summary>
internal sealed class PostgreSqlReadinessHealthCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<PostgreSqlReadinessHealthCheck> logger) : IHealthCheck
{
    private const string DatabaseUnreachable = "database_unreachable";
    private const string MigrationsPending = "migrations_pending";
    private const string DependencyFailure = "dependency_failure";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // DbContext resolution must remain inside this boundary. A missing connection string,
            // provider configuration failure, or service-resolution error is operationally "not
            // ready", rather than an FND-02 internal-error envelope.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            BankDbContext dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return Reject(DatabaseUnreachable);
            }

            IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(
                cancellationToken);

            return pending.Any()
                ? Reject(MigrationsPending)
                : HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            HealthLog.DependencyFailure(logger, DependencyFailure, exception.GetType().Name);
            return HealthCheckResult.Unhealthy();
        }
    }

    private HealthCheckResult Reject(string reason)
    {
        HealthLog.Rejected(logger, reason);
        return HealthCheckResult.Unhealthy();
    }
}

internal static partial class HealthLog
{
    // These methods accept an allow-list only: a fixed reason and a type name. They deliberately
    // never receive an exception object, message, stack trace, connection string, or credential.
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Error,
        Message = "Readiness rejected with {ReadinessReason}.")]
    public static partial void Rejected(ILogger logger, string readinessReason);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "Readiness rejected with {ReadinessReason}. Exception type: {ExceptionType}.")]
    public static partial void DependencyFailure(
        ILogger logger,
        string readinessReason,
        string exceptionType);
}
