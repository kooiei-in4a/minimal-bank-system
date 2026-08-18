using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using Npgsql;

namespace MinimalBankSystem.Api.OperatorMutation;

/// <summary>
/// PostgreSQL pessimistic-locking primitives for the active-administrator invariant (Issue #171
/// fixed contract). Locking only the target Operator row and doing a read-count-then-write is
/// explicitly insufficient per the approved contract: concurrent disable/demotion of two different
/// Operators that are each individually valid in isolation must still preserve at least one active
/// administrator after both commit. This module locks the full active-administrator set plus the
/// target row, in deterministic ascending Operator identifier order, before any invariant decision
/// is made, and every invariant decision reads the post-lock (not pre-lock) row values.
/// </summary>
internal static class OperatorMutationConcurrency
{
    public const string LockTimeout = "3000ms";

    public static async Task<IReadOnlyDictionary<Guid, Operator>> LockActiveAdministratorSetAndTargetAsync(
        BankDbContext persistence,
        Guid targetIdentifier,
        CancellationToken cancellationToken)
    {
        List<Guid> activeAdministratorIds = await persistence.Operators
            .AsNoTracking()
            .Where(candidate => candidate.Role == OperatorRole.Administrator && candidate.State == OperatorState.Active)
            .Select(candidate => candidate.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> orderedCandidateIds = activeAdministratorIds
            .Append(targetIdentifier)
            .Distinct()
            .Order()
            .ToList();

        return await LockRowsInOrderAsync(persistence, orderedCandidateIds, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyDictionary<Guid, Operator>> LockSingleRowAsync(
        BankDbContext persistence,
        Guid targetIdentifier,
        CancellationToken cancellationToken) =>
        await LockRowsInOrderAsync(persistence, [targetIdentifier], cancellationToken).ConfigureAwait(false);

    public static int CountActiveAdministrators(IReadOnlyDictionary<Guid, Operator> lockedRows) =>
        lockedRows.Values.Count(candidate =>
            candidate.Role == OperatorRole.Administrator && candidate.State == OperatorState.Active);

    public static bool IsLockConflict(Exception failure) =>
        UnwrapPostgresException(failure) is PostgresException
        {
            SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected,
        };

    private static async Task<IReadOnlyDictionary<Guid, Operator>> LockRowsInOrderAsync(
        BankDbContext persistence,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        // Rows must be locked one at a time, in the same deterministic order every caller uses. A
        // single multi-row `... ORDER BY id FOR UPDATE` query does not guarantee PostgreSQL
        // acquires the row locks themselves in that order (the LockRows plan node runs before
        // Sort), so ordering is enforced here by issuing one lock request per row.
        Dictionary<Guid, Operator> locked = [];
        foreach (Guid id in orderedIds)
        {
            Operator? row = await persistence.Operators
                .FromSqlInterpolated($"SELECT * FROM operators WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                locked[id] = row;
            }
        }

        return locked;
    }

    private static PostgresException? UnwrapPostgresException(Exception failure) => failure switch
    {
        PostgresException postgres => postgres,
        _ => failure.InnerException as PostgresException,
    };
}

/// <summary>
/// Narrow, DI-substitutable seam around the row-locking strategy so OPR-MUT-ADMIN-01 can prove the
/// active-administrator invariant against the exact insufficient implementation the approved
/// contract calls out by name ("target Operatorだけをlockしてread-count-then-writeする実装では
/// 不十分です") without touching this production implementation.
/// </summary>
internal interface IOperatorMutationLockStrategy
{
    Task<IReadOnlyDictionary<Guid, Operator>> LockAsync(
        BankDbContext persistence,
        Guid targetIdentifier,
        bool lockActiveAdministratorSet,
        CancellationToken cancellationToken);
}

internal sealed class ActiveAdministratorSetLockStrategy : IOperatorMutationLockStrategy
{
    public Task<IReadOnlyDictionary<Guid, Operator>> LockAsync(
        BankDbContext persistence,
        Guid targetIdentifier,
        bool lockActiveAdministratorSet,
        CancellationToken cancellationToken) =>
        lockActiveAdministratorSet
            ? OperatorMutationConcurrency.LockActiveAdministratorSetAndTargetAsync(
                persistence,
                targetIdentifier,
                cancellationToken)
            : OperatorMutationConcurrency.LockSingleRowAsync(persistence, targetIdentifier, cancellationToken);
}
