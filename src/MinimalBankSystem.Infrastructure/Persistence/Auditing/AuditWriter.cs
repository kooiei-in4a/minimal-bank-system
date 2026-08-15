using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

/// <summary>PostgreSQL-backed Product Audit writer using the single application DbContext.</summary>
public sealed class AuditWriter(
    BankDbContext context,
    IAuditOperationRegistry operationRegistry,
    ApplicationTime time) : IAuditWriter
{
    public async Task AppendInCallerTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IDbContextTransaction transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Product Audit state-changing success requires an explicit caller transaction.");

        try
        {
            await AppendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception persistenceFailure)
        {
            await RollBackAsync(transaction, persistenceFailure).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<TResult> AppendInSeparateTransactionBeforeSuccessAsync<TResult>(
        AuditWriteRequest request,
        TResult successResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A separate Product Audit transaction cannot start inside an existing transaction.");
        }

        await using IDbContextTransaction transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await AppendAsync(request, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception persistenceFailure)
        {
            await RollBackAsync(transaction, persistenceFailure).ConfigureAwait(false);
            throw;
        }

        return successResult;
    }

    private async Task AppendAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken)
    {
        operationRegistry.EnsureRegistered(request.OperationIdentifier);

        AuditRecord record = AuditRecord.Create(
            request.ActorIdentifier,
            request.ActorRole,
            request.OperationIdentifier,
            request.TargetIdentifier,
            request.Result,
            request.FailureBusinessErrorCode,
            request.CorrelationId,
            time.GetUtcNow());

        context.AuditRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RollBackAsync(
        IDbContextTransaction transaction,
        Exception persistenceFailure)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception rollbackFailure)
        {
            throw new AggregateException(
                "Product Audit persistence and required transaction rollback both failed.",
                persistenceFailure,
                rollbackFailure);
        }
    }
}
