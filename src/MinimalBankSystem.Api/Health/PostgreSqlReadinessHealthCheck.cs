using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.Health;

/// <summary>Checks whether PostgreSQL is reachable and the canonical EF migrations are complete.</summary>
internal sealed class PostgreSqlReadinessHealthCheck(BankDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        _ = healthCheckContext;

        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy();
            }

            IEnumerable<string> pendingMigrations =
                await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);

            return pendingMigrations.Any()
                ? HealthCheckResult.Unhealthy()
                : HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            // Dependency details stay inside the operational boundary. The public response is fixed.
            return HealthCheckResult.Unhealthy();
        }
    }
}
