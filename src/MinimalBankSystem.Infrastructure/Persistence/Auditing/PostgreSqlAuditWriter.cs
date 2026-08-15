using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

/// <summary>
/// PostgreSQL Product Audit writer. Deterministic failure injection is supplied only by
/// integration-test composition; production exposes no selectable failure path.
/// </summary>
public sealed class PostgreSqlAuditWriter(
    BankDbContext context,
    IAuditOperationRegistry operationRegistry,
    ApplicationTime applicationTime) : IAuditWriter
{
    private readonly object reservationGate = new();
    private readonly HashSet<AuditInvocationKey> reservations = [];

    public async Task AppendToCurrentTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Caller-transaction Audit persistence requires an active caller-owned transaction.");

        AuditInvocationKey? reservation = null;

        try
        {
            if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            {
                throw new InvalidOperationException(
                    "Product Audit caller transactions must use PostgreSQL READ COMMITTED isolation.");
            }

            AuditRecord record = CreateRecord(request);
            reservation = Reserve(record);
            context.AuditRecords.Add(record);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception persistenceFailure)
        {
            try
            {
                await RollBackRequiredTransactionAsync(transaction, persistenceFailure).ConfigureAwait(false);
            }
            finally
            {
                context.ChangeTracker.Clear();
                Release(reservation);
            }

            throw;
        }
    }

    public async Task<TResult> AppendInSeparateTransactionBeforeResultAsync<TResult>(
        AuditWriteRequest request,
        Func<CancellationToken, Task<TResult>> successResultFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(successResultFactory);

        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "Separate Product Audit persistence cannot run inside a caller transaction.");
        }

        if (context.ChangeTracker.Entries().Any(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Separate Product Audit persistence refuses unrelated pending entity changes.");
        }

        AuditInvocationKey? reservation = null;

        try
        {
            AuditRecord record = CreateRecord(request);
            reservation = Reserve(record);

            await using IDbContextTransaction transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                context.AuditRecords.Add(record);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception persistenceFailure)
            {
                await RollBackRequiredTransactionAsync(transaction, persistenceFailure).ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            context.ChangeTracker.Clear();
            Release(reservation);
            throw;
        }

        // The factory cannot create or expose a success value until the Audit commit completed.
        return await successResultFactory(cancellationToken).ConfigureAwait(false);
    }

    private AuditRecord CreateRecord(AuditWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        operationRegistry.EnsureRegistered(request.OperationIdentifier);

        return AuditRecord.Create(
            request.ActorIdentifier,
            request.ActorRole,
            request.OperationIdentifier,
            request.TargetIdentifier,
            request.Result,
            request.FailureBusinessErrorCode,
            request.CorrelationId,
            applicationTime.GetUtcNow());
    }

    private AuditInvocationKey Reserve(AuditRecord record)
    {
        AuditInvocationKey key = new(
            record.ActorIdentifier,
            record.OperationIdentifier,
            record.TargetIdentifier,
            record.CorrelationId);

        lock (reservationGate)
        {
            if (!reservations.Add(key))
            {
                throw new InvalidOperationException(
                    "A Product Audit record was already requested for this operation invocation.");
            }
        }

        return key;
    }

    private void Release(AuditInvocationKey? reservation)
    {
        if (reservation is null)
        {
            return;
        }

        lock (reservationGate)
        {
            reservations.Remove(reservation);
        }
    }

    private async Task RollBackRequiredTransactionAsync(
        IDbContextTransaction transaction,
        Exception persistenceFailure)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception rollbackFailure)
        {
            try
            {
                // Closing is a fail-closed fallback: PostgreSQL rolls back any still-open
                // transaction. The rollback failure remains visible in the aggregate below.
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
            catch (Exception closeFailure)
            {
                throw new AggregateException(
                    "Product Audit persistence failed; rollback and fail-closed connection shutdown also failed.",
                    persistenceFailure,
                    rollbackFailure,
                    closeFailure);
            }

            throw new AggregateException(
                "Product Audit persistence failed and the required transaction rollback failed; the connection was closed fail-closed.",
                persistenceFailure,
                rollbackFailure);
        }
    }

    private sealed record AuditInvocationKey(
        Guid ActorIdentifier,
        string OperationIdentifier,
        string TargetIdentifier,
        string CorrelationId);
}
