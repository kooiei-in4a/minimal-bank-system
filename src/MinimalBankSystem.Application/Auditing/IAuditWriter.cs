namespace MinimalBankSystem.Application.Auditing;

/// <summary>Reusable fail-closed Product Audit transaction primitives.</summary>
public interface IAuditWriter
{
    /// <summary>
    /// Appends using the current explicit caller transaction. Any validation or persistence failure
    /// rolls that transaction back before the failure is returned to the caller.
    /// </summary>
    Task AppendInCallerTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends and commits in a separate short transaction before returning the supplied success
    /// value. Failure throws instead of exposing that value.
    /// </summary>
    Task<TResult> AppendInSeparateTransactionBeforeSuccessAsync<TResult>(
        AuditWriteRequest request,
        TResult successResult,
        CancellationToken cancellationToken = default);
}
