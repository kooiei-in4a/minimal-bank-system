using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.OperatorMutation;

internal sealed class OperatorMutationService(
    BankDbContext persistence,
    ApplicationTime applicationTime,
    IOperatorMutationLockSession lockSession,
    ILastActiveAdministratorInvariant lastAdminInvariant,
    IOperatorMutationEffect mutationEffect,
    IOperatorMutationSuccessCommitter successCommitter) : IOperatorMutationService
{
    public async Task<OperatorMutationOutcome> ExecuteAsync(
        OperatorMutationKind kind,
        Guid operatorIdentifier,
        OperatorRole? requestedRole,
        CurrentOperatorSnapshot actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (kind == OperatorMutationKind.ChangeRole && requestedRole is null)
        {
            throw new ArgumentNullException(nameof(requestedRole));
        }

        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await lockSession.SetLockTimeoutAsync(persistence, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<Guid> lockedActiveAdministrators = await lockSession
                .LockActiveAdministratorIdentifiersAsync(persistence, cancellationToken)
                .ConfigureAwait(false);

            bool targetLocked = await lockSession
                .TryLockOperatorByIdAsync(persistence, operatorIdentifier, cancellationToken)
                .ConfigureAwait(false);
            if (!targetLocked)
            {
                return Reject(StatusCodes.Status404NotFound, ApiErrorEnvelope.OperatorNotFound);
            }

            Operator? target = await persistence.Operators
                .SingleOrDefaultAsync(
                    operatorEntity => operatorEntity.Id == operatorIdentifier,
                    cancellationToken)
                .ConfigureAwait(false);
            if (target is null)
            {
                return Reject(StatusCodes.Status404NotFound, ApiErrorEnvelope.OperatorNotFound);
            }

            if (IsNoOp(kind, target, requestedRole))
            {
                return Reject(
                    StatusCodes.Status409Conflict,
                    ApiErrorEnvelope.StateTransitionNotAllowed);
            }

            if (kind == OperatorMutationKind.Disable && target.Id == actor.Identifier)
            {
                return Reject(
                    StatusCodes.Status409Conflict,
                    ApiErrorEnvelope.StateTransitionNotAllowed);
            }

            if (lastAdminInvariant.WouldBeViolated(
                    target,
                    kind,
                    requestedRole,
                    lockedActiveAdministrators))
            {
                return Reject(
                    StatusCodes.Status409Conflict,
                    ApiErrorEnvelope.StateTransitionNotAllowed);
            }

            DateTimeOffset utcNow = applicationTime.GetUtcNow();
            string securityStamp = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            Apply(kind, target, requestedRole, utcNow, securityStamp);

            AuditWriteRequest successAudit = new(
                actor.Identifier,
                actor.Role,
                OperationIdentifier(kind),
                operatorIdentifier.ToString("D"),
                AuditResult.Success,
                FailureBusinessErrorCode: null,
                correlationId);

            await successCommitter
                .CommitAsync(transaction, successAudit, cancellationToken)
                .ConfigureAwait(false);

            return new OperatorMutationOutcome.Success(target);
        }
        catch (Exception exception) when (OperatorMutationLocking.IsLockTimeoutOrDeadlock(exception))
        {
            persistence.ChangeTracker.Clear();
            return Reject(
                StatusCodes.Status409Conflict,
                ApiErrorEnvelope.ConcurrentOperationConflict);
        }
        catch
        {
            persistence.ChangeTracker.Clear();
            throw;
        }
    }

    private void Apply(
        OperatorMutationKind kind,
        Operator target,
        OperatorRole? requestedRole,
        DateTimeOffset utcNow,
        string securityStamp)
    {
        switch (kind)
        {
            case OperatorMutationKind.Enable:
                mutationEffect.Enable(target, utcNow, securityStamp);
                break;
            case OperatorMutationKind.Disable:
                mutationEffect.Disable(target, utcNow, securityStamp);
                break;
            case OperatorMutationKind.ChangeRole:
                mutationEffect.ChangeRole(
                    target,
                    requestedRole
                        ?? throw new InvalidOperationException("A role-change mutation requires a requested role."),
                    utcNow,
                    securityStamp);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Operator mutation kind.");
        }
    }

    private static bool IsNoOp(
        OperatorMutationKind kind,
        Operator target,
        OperatorRole? requestedRole) =>
        kind switch
        {
            OperatorMutationKind.Enable => target.State == OperatorState.Active,
            OperatorMutationKind.Disable => target.State == OperatorState.Disabled,
            OperatorMutationKind.ChangeRole => target.Role == requestedRole,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Operator mutation kind."),
        };

    internal static string OperationIdentifier(OperatorMutationKind kind) => kind switch
    {
        OperatorMutationKind.Enable => OperatorMutationAudit.EnableOperationIdentifier,
        OperatorMutationKind.Disable => OperatorMutationAudit.DisableOperationIdentifier,
        OperatorMutationKind.ChangeRole => OperatorMutationAudit.ChangeRoleOperationIdentifier,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Operator mutation kind."),
    };

    private static OperatorMutationOutcome.Rejection Reject(int statusCode, ApiErrorEnvelope error) =>
        new(statusCode, error);
}

internal sealed class PostgreSqlOperatorMutationLockSession : IOperatorMutationLockSession
{
    public Task SetLockTimeoutAsync(BankDbContext persistence, CancellationToken cancellationToken) =>
        OperatorMutationLocking.SetTransactionLockTimeoutAsync(persistence, cancellationToken);

    public Task<IReadOnlyList<Guid>> LockActiveAdministratorIdentifiersAsync(
        BankDbContext persistence,
        CancellationToken cancellationToken) =>
        OperatorMutationLocking.LockActiveAdministratorIdentifiersAsync(persistence, cancellationToken);

    public Task<bool> TryLockOperatorByIdAsync(
        BankDbContext persistence,
        Guid operatorId,
        CancellationToken cancellationToken) =>
        OperatorMutationLocking.TryLockOperatorByIdAsync(persistence, operatorId, cancellationToken);
}

internal sealed class LockedSetLastActiveAdministratorInvariant : ILastActiveAdministratorInvariant
{
    public bool WouldBeViolated(
        Operator target,
        OperatorMutationKind kind,
        OperatorRole? requestedRole,
        IReadOnlyList<Guid> lockedActiveAdministratorIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(lockedActiveAdministratorIdentifiers);

        if (target.State != OperatorState.Active || target.Role != OperatorRole.Administrator)
        {
            return false;
        }

        bool removesFromActiveAdministratorSet = kind switch
        {
            OperatorMutationKind.Disable => true,
            OperatorMutationKind.ChangeRole => requestedRole != OperatorRole.Administrator,
            OperatorMutationKind.Enable => false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Operator mutation kind."),
        };

        return ActiveAdministratorSet.MutationWouldLeaveZeroActiveAdministrators(
            target,
            removesFromActiveAdministratorSet,
            lockedActiveAdministratorIdentifiers);
    }
}

internal sealed class SecurityInvalidatingOperatorMutationEffect : IOperatorMutationEffect
{
    public void Enable(Operator target, DateTimeOffset utcNow, string securityStamp)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Enable(utcNow, securityStamp);
    }

    public void Disable(Operator target, DateTimeOffset utcNow, string securityStamp)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Disable(utcNow, securityStamp);
    }

    public void ChangeRole(Operator target, OperatorRole role, DateTimeOffset utcNow, string securityStamp)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.ChangeRole(role, utcNow, securityStamp);
    }
}

internal sealed class AtomicOperatorMutationSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        IDbContextTransaction transaction,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(successAudit);

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await auditWriter.AppendToCurrentTransactionAsync(successAudit, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
