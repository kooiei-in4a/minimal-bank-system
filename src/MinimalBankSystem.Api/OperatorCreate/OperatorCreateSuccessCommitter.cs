using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.OperatorCreate;

/// <summary>
/// Commits a newly constructed Operator and its required success Audit as one atomic unit. This
/// is a narrow, DI-substitutable seam so OPR-CREATE-AUD-01 can prove the atomicity invariant
/// against a deliberately non-atomic test double without touching this production
/// implementation (test-composition-only technique, matching the approach already established by
/// WP2-AUTHZ-01's Critical Mutation tests).
/// </summary>
internal interface IOperatorCreateSuccessCommitter
{
    Task CommitAsync(
        Operator newOperator,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

internal sealed class AtomicOperatorCreateSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorCreateSuccessCommitter
{
    public async Task CommitAsync(
        Operator newOperator,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newOperator);
        ArgumentNullException.ThrowIfNull(httpContext);

        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            persistence.Operators.Add(newOperator);
            await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AuditWriteRequest successAudit = new(
                actorIdentifier,
                actorRole,
                OperatorCreateAudit.OperationIdentifier,
                newOperator.Id.ToString("D"),
                AuditResult.Success,
                FailureBusinessErrorCode: null,
                httpContext.TraceIdentifier);

            // WP2-AUD-01's writer rolls back this shared ambient transaction and rethrows if the
            // required success Audit cannot be persisted, so the Operator insert above never
            // commits without it. If the Operator SaveChangesAsync above throws instead (for
            // example a concurrently-registered login identifier), `transaction` is still open
            // and uncommitted; the `await using` rolls it back on the way out either way.
            await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A failed SaveChangesAsync leaves the Operator entry tracked as Added. The caller
            // may still need this DbContext for a clean handler-rejection Audit write (e.g. a
            // concurrently-lost duplicate-login-identifier race), which requires no pending
            // entity changes, so this failure path always clears the tracker before propagating.
            persistence.ChangeTracker.Clear();
            throw;
        }
    }
}
