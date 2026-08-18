using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;

namespace MinimalBankSystem.Api.OperatorMutation;

internal enum OperatorMutationKind
{
    Enable = 0,
    Disable = 1,
    ChangeRole = 2,
}

internal abstract record OperatorMutationOutcome
{
    internal sealed record Success(Operator Target) : OperatorMutationOutcome;

    internal sealed record Rejection(int StatusCode, ApiErrorEnvelope Error) : OperatorMutationOutcome;
}

internal interface IOperatorMutationService
{
    Task<OperatorMutationOutcome> ExecuteAsync(
        OperatorMutationKind kind,
        Guid operatorIdentifier,
        OperatorRole? requestedRole,
        CurrentOperatorSnapshot actor,
        string correlationId,
        CancellationToken cancellationToken);
}

internal interface IOperatorMutationLockSession
{
    Task SetLockTimeoutAsync(BankDbContext persistence, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> LockActiveAdministratorIdentifiersAsync(
        BankDbContext persistence,
        CancellationToken cancellationToken);

    Task<bool> TryLockOperatorByIdAsync(
        BankDbContext persistence,
        Guid operatorId,
        CancellationToken cancellationToken);
}

internal interface ILastActiveAdministratorInvariant
{
    bool WouldBeViolated(
        Operator target,
        OperatorMutationKind kind,
        OperatorRole? requestedRole,
        IReadOnlyList<Guid> lockedActiveAdministratorIdentifiers);
}

internal interface IOperatorMutationEffect
{
    void Enable(Operator target, DateTimeOffset utcNow, string securityStamp);

    void Disable(Operator target, DateTimeOffset utcNow, string securityStamp);

    void ChangeRole(Operator target, OperatorRole role, DateTimeOffset utcNow, string securityStamp);
}

internal interface IOperatorMutationSuccessCommitter
{
    Task CommitAsync(
        IDbContextTransaction transaction,
        AuditWriteRequest successAudit,
        CancellationToken cancellationToken);
}
