using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Auditing;

/// <summary>
/// Reusable fail-closed transaction primitives. Required Audit failures escape to the API error
/// boundary; no primitive returns a business/query result before its Audit transaction commits.
/// </summary>
public sealed class AuditTransactionRunner(BankDbContext dbContext, AuditWriter writer)
{
    public async Task<TResult> ExecuteStateChangingAsync<TResult>(
        Func<BankDbContext, CancellationToken, Task<TResult>> stateChange,
        Func<TResult, AuditWriteRequest> auditRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateChange);
        ArgumentNullException.ThrowIfNull(auditRequest);

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            TResult result = await stateChange(dbContext, cancellationToken).ConfigureAwait(false);
            await writer.AppendInCallerTransactionAsync(auditRequest(result), cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<TResult> ExecuteAuditedQueryAsync<TResult>(
        Func<CancellationToken, Task<TResult>> query,
        Func<TResult, AuditWriteRequest> auditRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(auditRequest);

        TResult result = await query(cancellationToken).ConfigureAwait(false);
        await AppendInSeparateShortTransactionAsync(auditRequest(result), cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task AppendInSeparateShortTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await writer.AppendInCallerTransactionAsync(request, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
