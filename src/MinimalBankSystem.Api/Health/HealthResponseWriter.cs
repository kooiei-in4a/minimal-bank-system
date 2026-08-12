using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MinimalBankSystem.Api.Health;

internal static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";

        string response = report.Status == HealthStatus.Healthy
            ? "Healthy"
            : "Unhealthy";

        return context.Response.WriteAsync(response, context.RequestAborted);
    }
}
