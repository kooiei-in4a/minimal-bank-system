using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;

namespace MinimalBankSystem.Infrastructure.Persistence.Auditing;

/// <summary>
/// PostgreSQL Product Audit writer. Failure injection is supplied only through test-composed EF
/// interceptors; this production implementation exposes no configuration or request-controlled
/// failure switch.
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
        catch
        {
            await TryRollbackAsync(transaction).ConfigureAwait(false);
            context.ChangeTracker.Clear();
            Release(reservation);
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
            catch
            {
                await TryRollbackAsync(transaction).ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            context.ChangeTracker.Clear();
            Release(reservation);
            throw;
        }

        // This is intentionally after the committed transaction scope. The API makes it
        // impossible for a caller to obtain the success payload from this primitive first.
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

    private async Task TryRollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception rollbackException)
        {
            try
            {
                // Closing a PostgreSQL connection is the fail-closed fallback: PostgreSQL rolls
                // back any still-open transaction instead of leaving caller-side writes
                // committable after a required Audit failure.
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
            catch (Exception closeException)
            {
                throw new AggregateException(
                    "Product Audit failed and the caller transaction could not be rolled back or closed.",
                    rollbackException,
                    closeException);
            }
        }
    }

    private sealed record AuditInvocationKey(
        Guid ActorIdentifier,
        string OperationIdentifier,
        string TargetIdentifier,
        string CorrelationId);
}
