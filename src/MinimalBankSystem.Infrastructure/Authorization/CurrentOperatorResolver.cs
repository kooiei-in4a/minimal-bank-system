using Microsoft.EntityFrameworkCore;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Infrastructure.Authorization;

/// <summary>
/// Resolves the current persisted Operator snapshot for an authenticated request principal.
/// The JWT carries only the authorization-state version snapshot; the current DB row is the
/// authority for active state, authorization-state version and role (ADR-0007).
/// </summary>
public sealed class CurrentOperatorResolver(BankDbContext dbContext)
{
    public async Task<CurrentOperatorResolution> ResolveAsync(
        Guid operatorId,
        int presentedAuthorizationStateVersion,
        CancellationToken cancellationToken)
    {
        Operator? current = await dbContext.Operators
            .AsNoTracking()
            .SingleOrDefaultAsync(
                operatorEntity => operatorEntity.Id == operatorId,
                cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            return CurrentOperatorResolution.NotFound;
        }

        if (current.State is not OperatorState.Active)
        {
            return CurrentOperatorResolution.Disabled;
        }

        if (current.AuthorizationStateVersion != presentedAuthorizationStateVersion)
        {
            return CurrentOperatorResolution.AuthorizationStateVersionMismatch;
        }

        return CurrentOperatorResolution.Success(current);
    }
}

public enum CurrentOperatorResolutionStatus
{
    Success,
    OperatorNotFound,
    OperatorDisabled,
    AuthorizationStateVersionMismatch,
}

public sealed record CurrentOperatorResolution(
    CurrentOperatorResolutionStatus Status,
    Operator? Operator)
{
    public static CurrentOperatorResolution NotFound { get; } =
        new(CurrentOperatorResolutionStatus.OperatorNotFound, Operator: null);

    public static CurrentOperatorResolution Disabled { get; } =
        new(CurrentOperatorResolutionStatus.OperatorDisabled, Operator: null);

    public static CurrentOperatorResolution AuthorizationStateVersionMismatch { get; } =
        new(CurrentOperatorResolutionStatus.AuthorizationStateVersionMismatch, Operator: null);

    public static CurrentOperatorResolution Success(Operator operatorEntity) =>
        new(CurrentOperatorResolutionStatus.Success, operatorEntity);
}
