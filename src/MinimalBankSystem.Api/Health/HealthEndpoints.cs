using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.Health;

public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Plain-text bodies keep UseStatusCodePages from rewriting operational health failures
        // into the FND-02 business error envelope (503 is also unmapped there).
        endpoints.MapGet(LivePath, () => Results.Text("Healthy", "text/plain"));
        endpoints.MapGet(ReadyPath, CheckReadyAsync);
        return endpoints;
    }

    private static async Task<IResult> CheckReadyAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            BankDbContext dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

            // Connectivity is exercised by EF's pending-migration query against the
            // canonical history table. Pending rows mean traffic must not be accepted.
            string[] pendingMigrations =
            [
                .. await dbContext.Database.GetPendingMigrationsAsync(cancellationToken),
            ];

            if (pendingMigrations.Length > 0)
            {
                return Unhealthy();
            }

            return Results.Text("Healthy", "text/plain");
        }
        catch (Exception)
        {
            // Operational readiness failure only. Never surface connection strings,
            // credentials, exception types, messages, or stack traces.
            return Unhealthy();
        }
    }

    private static IResult Unhealthy() =>
        Results.Text("Unhealthy", "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable);
}
