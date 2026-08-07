using MinimalBankSystem.Application.Time;

namespace MinimalBankSystem.Api.Runtime;

internal static class ContractVerificationEndpoints
{
    public static IEndpointRouteBuilder MapContractVerificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/__contract__");

        group.MapGet("/time", (ApplicationTime applicationTime, ILoggerFactory loggerFactory, HttpContext context) =>
        {
            ILogger logger = loggerFactory.CreateLogger("ContractVerification");
            string? correlationId = context.Items[CorrelationId.HttpContextItemKey] as string;
            if (correlationId is not null && logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CA1848
                logger.LogInformation(
                    "Contract time probe correlation {CorrelationId}",
                    correlationId);
#pragma warning restore CA1848
            }
            else
            {
                ApiTechnicalLogMessages.LogContractTimeProbe(logger);
            }

            return Results.Json(new { utc = applicationTime.GetUtcNow() });
        });

        group.MapGet("/unmapped-exception", static () =>
        {
            throw new InvalidOperationException("contract-unmapped-sentinel-detail");
        });

        group.MapGet("/log-sentinel", (ILoggerFactory loggerFactory) =>
        {
            ILogger logger = loggerFactory.CreateLogger("ContractVerification");
            if (logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CA1848
                logger.Log(
                    LogLevel.Information,
                    default,
                    new Dictionary<string, object?>
                    {
                        ["password"] = ContractSentinels.Password,
                        ["jwt"] = ContractSentinels.Jwt,
                        ["signing_key"] = ContractSentinels.SigningKey,
                        ["idempotency_key"] = ContractSentinels.IdempotencyKey,
                        ["connection_string"] = ContractSentinels.ConnectionString,
                        ["probe"] = "allowed",
                    },
                    null,
                    static (_, _) => "Contract sentinel probe");
#pragma warning restore CA1848
            }

            return Results.Ok();
        });

        return endpoints;
    }
}

internal static class ContractSentinels
{
    public const string Password = "sentinel-password-value";
    public const string Jwt = "sentinel-jwt-token-value";
    public const string SigningKey = "sentinel-signing-key-value";
    public const string IdempotencyKey = "sentinel-idempotency-key-value";
    public const string ConnectionString = "sentinel-connection-string-value";
}
