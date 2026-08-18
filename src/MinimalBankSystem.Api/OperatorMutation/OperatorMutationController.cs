using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.OperatorMutation;

[ApiController]
[Route("operators")]
public sealed class OperatorMutationController(
    BankDbContext persistence,
    IAuditWriter auditWriter,
    ApplicationTime applicationTime) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorEnableAuthorizationAuditContext]
    [HttpPost("{operatorIdentifier:guid}/enable")]
    public Task<IActionResult> Enable(Guid operatorIdentifier, CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        return ExecuteAsync(
            operatorIdentifier,
            OperatorMutationAudit.EnableOperationIdentifier,
            lockActiveAdministratorSet: false,
            actor,
            (target, _, _) => target.State == OperatorState.Active
                ? MutationDecision.Reject(ApiErrorEnvelope.StateTransitionNotAllowed, StatusCodes.Status409Conflict)
                : MutationDecision.Proceed(o => o.Enable(applicationTime.GetUtcNow(), NewSecurityStamp())),
            cancellationToken);
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorDisableAuthorizationAuditContext]
    [HttpPost("{operatorIdentifier:guid}/disable")]
    public Task<IActionResult> Disable(Guid operatorIdentifier, CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        return ExecuteAsync(
            operatorIdentifier,
            OperatorMutationAudit.DisableOperationIdentifier,
            lockActiveAdministratorSet: true,
            actor,
            (target, activeAdministratorCount, currentActor) =>
            {
                if (target.Id == currentActor.Identifier)
                {
                    return MutationDecision.Reject(
                        ApiErrorEnvelope.StateTransitionNotAllowed,
                        StatusCodes.Status409Conflict);
                }

                if (target.State == OperatorState.Disabled)
                {
                    return MutationDecision.Reject(
                        ApiErrorEnvelope.StateTransitionNotAllowed,
                        StatusCodes.Status409Conflict);
                }

                if (target.Role == OperatorRole.Administrator && activeAdministratorCount <= 1)
                {
                    return MutationDecision.Reject(
                        ApiErrorEnvelope.StateTransitionNotAllowed,
                        StatusCodes.Status409Conflict);
                }

                return MutationDecision.Proceed(o => o.Disable(applicationTime.GetUtcNow(), NewSecurityStamp()));
            },
            cancellationToken);
    }

    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorRoleChangeAuthorizationAuditContext]
    [HttpPost("{operatorIdentifier:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid operatorIdentifier,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] OperatorRoleChangeRequest? request,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();
        OperatorRole? requestedRole = ParseRoleToken(request?.Role);

        if (requestedRole is null)
        {
            return await RejectAsync(
                    operatorIdentifier.ToString("D", CultureInfo.InvariantCulture),
                    OperatorMutationAudit.RoleChangeOperationIdentifier,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ExecuteAsync(
                operatorIdentifier,
                OperatorMutationAudit.RoleChangeOperationIdentifier,
                lockActiveAdministratorSet: true,
                actor,
                (target, activeAdministratorCount, _) =>
                {
                    if (target.Role == requestedRole.Value)
                    {
                        return MutationDecision.Reject(
                            ApiErrorEnvelope.StateTransitionNotAllowed,
                            StatusCodes.Status409Conflict);
                    }

                    bool isActiveAdministratorDemotion =
                        target.Role == OperatorRole.Administrator &&
                        target.State == OperatorState.Active &&
                        requestedRole.Value != OperatorRole.Administrator;

                    if (isActiveAdministratorDemotion && activeAdministratorCount <= 1)
                    {
                        return MutationDecision.Reject(
                            ApiErrorEnvelope.StateTransitionNotAllowed,
                            StatusCodes.Status409Conflict);
                    }

                    return MutationDecision.Proceed(
                        o => o.ChangeRole(requestedRole.Value, applicationTime.GetUtcNow(), NewSecurityStamp()));
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteAsync(
        Guid operatorIdentifier,
        string operationIdentifier,
        bool lockActiveAdministratorSet,
        CurrentOperatorSnapshot actor,
        Func<Operator, int, CurrentOperatorSnapshot, MutationDecision> decide,
        CancellationToken cancellationToken)
    {
        string targetIdentifier = operatorIdentifier.ToString("D", CultureInfo.InvariantCulture);

        IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            IReadOnlyDictionary<Guid, Operator> lockedRows;
            try
            {
                await persistence.Database
                    .ExecuteSqlRawAsync(
                        $"SET LOCAL lock_timeout = '{OperatorMutationConcurrency.LockTimeout}'",
                        cancellationToken)
                    .ConfigureAwait(false);

                lockedRows = await HttpContext.RequestServices
                    .GetRequiredService<IOperatorMutationLockStrategy>()
                    .LockAsync(persistence, operatorIdentifier, lockActiveAdministratorSet, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure) when (OperatorMutationConcurrency.IsLockConflict(failure))
            {
                await RollBackAndClearAsync(transaction, cancellationToken).ConfigureAwait(false);
                return await RejectAsync(
                        targetIdentifier,
                        operationIdentifier,
                        ApiErrorEnvelope.ConcurrentOperationConflict,
                        StatusCodes.Status409Conflict,
                        actor,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!lockedRows.TryGetValue(operatorIdentifier, out Operator? target))
            {
                await RollBackAndClearAsync(transaction, cancellationToken).ConfigureAwait(false);
                return await RejectAsync(
                        targetIdentifier,
                        operationIdentifier,
                        ApiErrorEnvelope.OperatorNotFound,
                        StatusCodes.Status404NotFound,
                        actor,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            int activeAdministratorCount = OperatorMutationConcurrency.CountActiveAdministrators(lockedRows);
            MutationDecision decision = decide(target, activeAdministratorCount, actor);

            if (!decision.ShouldProceed)
            {
                await RollBackAndClearAsync(transaction, cancellationToken).ConfigureAwait(false);
                return await RejectAsync(
                        targetIdentifier,
                        operationIdentifier,
                        decision.RejectionError!,
                        decision.RejectionStatusCode,
                        actor,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            AuditWriteRequest successAudit = new(
                actor.Identifier,
                actor.Role,
                operationIdentifier,
                targetIdentifier,
                AuditResult.Success,
                FailureBusinessErrorCode: null,
                HttpContext.TraceIdentifier);

            await HttpContext.RequestServices
                .GetRequiredService<IOperatorMutationSuccessCommitter>()
                .CommitAsync(persistence, transaction, target, decision.ApplyMutation!, successAudit, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new OperatorMutationResponse(
                target.Id,
                ToStateToken(target.State),
                ToRoleToken(target.Role)));
        }
        catch
        {
            persistence.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RollBackAndClearAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        persistence.ChangeTracker.Clear();
    }

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator mutation requires a current Product-Audit actor.");

    private async Task<IActionResult> RejectAsync(
        string targetIdentifier,
        string operationIdentifier,
        ApiErrorEnvelope error,
        int statusCode,
        CurrentOperatorSnapshot actor,
        CancellationToken cancellationToken)
    {
        AuditWriteRequest rejectionAudit = new(
            actor.Identifier,
            actor.Role,
            operationIdentifier,
            targetIdentifier,
            AuditResult.Failure,
            error.Code,
            HttpContext.TraceIdentifier);

        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                rejectionAudit,
                _ => Task.FromResult<IActionResult>(StatusCode(statusCode, error)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private static OperatorRole? ParseRoleToken(string? token) => token switch
    {
        OperatorPersistence.AdministratorRoleToken => OperatorRole.Administrator,
        OperatorPersistence.TellerRoleToken => OperatorRole.Teller,
        OperatorPersistence.ViewerRoleToken => OperatorRole.Viewer,
        _ => null,
    };

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => OperatorPersistence.ActiveStateToken,
        OperatorState.Disabled => OperatorPersistence.DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "Unknown Operator state cannot be exposed by the mutation API."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Unknown Operator role cannot be exposed by the mutation API."),
    };

    private readonly struct MutationDecision
    {
        private MutationDecision(
            bool shouldProceed,
            ApiErrorEnvelope? rejectionError,
            int rejectionStatusCode,
            Action<Operator>? applyMutation)
        {
            ShouldProceed = shouldProceed;
            RejectionError = rejectionError;
            RejectionStatusCode = rejectionStatusCode;
            ApplyMutation = applyMutation;
        }

        public bool ShouldProceed { get; }

        public ApiErrorEnvelope? RejectionError { get; }

        public int RejectionStatusCode { get; }

        public Action<Operator>? ApplyMutation { get; }

        public static MutationDecision Reject(ApiErrorEnvelope error, int statusCode) =>
            new(false, error, statusCode, null);

        public static MutationDecision Proceed(Action<Operator> applyMutation) =>
            new(true, null, 0, applyMutation);
    }
}

public sealed record OperatorRoleChangeRequest(string? Role);

/// <summary>
/// Deliberately closed Operator mutation success projection. Credential, security and
/// authorization-state fields remain unavailable to MVC serialization because they are not part
/// of this type.
/// </summary>
public sealed record OperatorMutationResponse(Guid OperatorIdentifier, string State, string Role);
