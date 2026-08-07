namespace MinimalBankSystem.Api.Runtime;

/// <summary>
/// Technical logging baseline for the API.
/// </summary>
public static class ApiTechnicalLoggingExtensions
{
    /// <summary>
    /// Configures the technical (failure diagnosis) logging baseline required by ADR-0008:
    /// <c>Microsoft.Extensions.Logging</c> with JSON console output and correlation scopes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prohibited field policy: technical log events are built from a fixed set of diagnostic fields
    /// (correlation identifier, fixed error code, HTTP status, request method and request path).
    /// Request bodies, request headers, query strings and configuration values are never written, so
    /// passwords, JWTs, signing keys, raw idempotency keys and connection strings have no route into
    /// log output. Secrets are excluded from the log event rather than masked inside it, because a
    /// value that is never recorded cannot leak through an incomplete mask.
    /// </para>
    /// <para>
    /// Framework request logging is limited to <see cref="LogLevel.Warning"/> because its
    /// informational messages contain the full request URL including the query string.
    /// </para>
    /// <para>
    /// This baseline is the technical log of specification section 14.3. It is not the Audit Log of
    /// section 14.2, which is persisted separately.
    /// </para>
    /// </remarks>
    public static ILoggingBuilder AddApiTechnicalLogging(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        // Console output must be machine readable end to end, so no additional provider may write
        // unstructured lines to the same stream.
        logging.ClearProviders();

        logging.AddJsonConsole(static options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
        });

        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        return logging;
    }
}
