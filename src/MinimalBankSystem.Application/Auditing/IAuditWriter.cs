namespace MinimalBankSystem.Application.Auditing;

/// <summary>Reusable Product Audit transaction primitives supplied by WP2-AUD-01.</summary>
public interface IAuditWriter
{
    /// <summary>
    /// Appends within an active caller-owned READ COMMITTED transaction. The writer never commits
    /// that transaction and rolls it back before propagating a required Audit failure.
    /// </summary>
    Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a short Audit-only transaction before invoking the success-result factory. If
    /// Audit persistence fails, the factory is not invoked and no success payload is exposed.
    /// </summary>
    Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default);
}
