using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Auditing;

/// <summary>Appends one required Product Audit row inside an already-active caller transaction.</summary>
public sealed class AuditWriter(
    BankDbContext dbContext,
    AuditOperationRegistry operationRegistry,
    TimeProvider timeProvider)
{
    public async Task<AuditRecord> AppendInCallerTransactionAsync(
        AuditWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Required Product Audit persistence needs an explicit caller transaction.");
        }

        operationRegistry.EnsureRegistered(request.OperationIdentifier);

        AuditRecord record = AuditRecord.Create(
            request.ActorIdentifier,
            request.ActorRole,
            request.OperationIdentifier,
            request.TargetIdentifier,
            request.Result,
            request.FailureBusinessErrorCode,
            request.CorrelationId,
            timeProvider.GetUtcNow());

        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }
}
