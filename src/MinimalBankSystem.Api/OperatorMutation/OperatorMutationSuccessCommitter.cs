using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.OperatorMutation;

/// <summary>
/// Commits an already-decided Operator lifecycle mutation and its required success Audit as one
/// atomic unit against an already-open, already-lock-holding caller transaction. This is a narrow,
/// DI-substitutable seam so OPR-MUT-AUD-01 can prove the atomicity invariant against a
/// deliberately non-atomic test double without touching this production implementation (the same
/// test-composition-only technique already established by OPR-CREATE-AUD-01).
/// </summary>
internal interface IOperatorMutationSuccessCommitter
{
    Task CommitAsync(
        BankDbContext persistence,
        IDbContextTransaction transaction,
        Operator target,
        Action<Operator> applyMutation,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken);
}

internal sealed class AtomicOperatorMutationSuccessCommitter(IAuditWriter auditWriter)
    : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        BankDbContext persistence,
        IDbContextTransaction transaction,
        Operator target,
        Action<Operator> applyMutation,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(applyMutation);

        applyMutation(target);
        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // WP2-AUD-01's writer rolls back this shared ambient transaction and rethrows if the
        // required success Audit cannot be persisted, so the Operator state/role change above
        // never commits without it.
        await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
