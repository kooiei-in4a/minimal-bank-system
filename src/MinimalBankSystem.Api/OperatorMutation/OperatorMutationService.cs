using System.Data;
using System.Data.Common;
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
using Npgsql;

namespace MinimalBankSystem.Api.OperatorMutation;

public enum OperatorMutationKind
{
    Enable,
    Disable,
    ChangeRole,
}

public interface IOperatorMutationService
{
    Task<OperatorMutationResult> ExecuteAsync(
        Guid operatorIdentifier,
        OperatorMutationKind operation,
        OperatorRole? requestedRole,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

public sealed record OperatorMutationResult(
    OperatorMutationResponse? Success,
    ApiErrorEnvelope? Error,
    int StatusCode)
{
    public static OperatorMutationResult Succeeded(OperatorMutationResponse response) =>
        new(response, null, StatusCodes.Status200OK);

    public static OperatorMutationResult Rejected(ApiErrorEnvelope error, int statusCode) =>
        new(null, error, statusCode);
}

internal interface IOperatorMutationSuccessCommitter
{
    Task CommitAsync(
        Operator target,
        OperatorMutationKind operation,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken);
}

internal sealed class OperatorMutationService(
    BankDbContext persistence,
    IOperatorMutationSuccessCommitter successCommitter,
    ApplicationTime applicationTime) : IOperatorMutationService
{
    private const int LockCommandTimeoutSeconds = 10;

    public async Task<OperatorMutationResult> ExecuteAsync(
        Guid operatorIdentifier,
        OperatorMutationKind operation,
        OperatorRole? requestedRole,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> lockedActiveAdministrators;
        long activeAdministratorCount;
        try
        {
            await SetLocalLockTimeoutAsync(cancellationToken).ConfigureAwait(false);
            lockedActiveAdministrators = await LockActiveAdministratorsAsync(cancellationToken)
                .ConfigureAwait(false);
            activeAdministratorCount = await CountActiveAdministratorsAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!lockedActiveAdministrators.Contains(operatorIdentifier))
            {
                await LockTargetOperatorAsync(operatorIdentifier, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PostgresException failure) when (IsConcurrencyFailure(failure))
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            return OperatorMutationResult.Rejected(
                ApiErrorEnvelope.ConcurrentOperationConflict,
                StatusCodes.Status409Conflict);
        }

        Operator? target = await persistence.Operators
            .SingleOrDefaultAsync(operatorEntity => operatorEntity.Id == operatorIdentifier, cancellationToken)
            .ConfigureAwait(false);

        if (target is null)
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            return OperatorMutationResult.Rejected(
                ApiErrorEnvelope.OperatorNotFound,
                StatusCodes.Status404NotFound);
        }

        OperatorState desiredState = operation == OperatorMutationKind.Enable
            ? OperatorState.Active
            : operation == OperatorMutationKind.Disable
                ? OperatorState.Disabled
                : target.State;
        OperatorRole desiredRole = operation == OperatorMutationKind.ChangeRole
            ? requestedRole ?? throw new InvalidOperationException("Role mutation requires a validated role.")
            : target.Role;

        ApiErrorEnvelope? rejection = GetRejection(
            target,
            operation,
            desiredState,
            desiredRole,
            actorIdentifier,
            activeAdministratorCount);
        if (rejection is not null)
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            return OperatorMutationResult.Rejected(rejection, StatusCodes.Status409Conflict);
        }

        target.ApplyLifecycleMutation(
            desiredState,
            desiredRole,
            applicationTime.GetUtcNow(),
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await successCommitter
            .CommitAsync(
                target,
                operation,
                actorIdentifier,
                actorRole,
                httpContext,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        return OperatorMutationResult.Succeeded(ToResponse(target));
    }

    private static ApiErrorEnvelope? GetRejection(
        Operator target,
        OperatorMutationKind operation,
        OperatorState desiredState,
        OperatorRole desiredRole,
        Guid actorIdentifier,
        long activeAdministratorCount)
    {
        if (operation == OperatorMutationKind.Disable && actorIdentifier == target.Id)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        bool isNoOp = target.State == desiredState && target.Role == desiredRole;
        if (isNoOp)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        bool losesActiveAdministrator =
            target.State == OperatorState.Active &&
            target.Role == OperatorRole.Administrator &&
            (desiredState != OperatorState.Active || desiredRole != OperatorRole.Administrator);
        if (losesActiveAdministrator && activeAdministratorCount <= 1)
        {
            return ApiErrorEnvelope.StateTransitionNotAllowed;
        }

        return null;
    }

    private async Task<HashSet<Guid>> LockActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            $"""
             SELECT {OperatorPersistence.IdColumn}
             FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
               AND {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}'
             ORDER BY {OperatorPersistence.IdColumn} ASC
             FOR UPDATE;
             """);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        HashSet<Guid> identifiers = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            identifiers.Add(reader.GetGuid(0));
        }

        return identifiers;
    }

    private async Task<long> CountActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            $"""
             SELECT count(*)
             FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.StateColumn} = '{OperatorPersistence.ActiveStateToken}'
               AND {OperatorPersistence.FixedRoleColumn} = '{OperatorPersistence.AdministratorRoleToken}';
             """);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private async Task LockTargetOperatorAsync(Guid operatorIdentifier, CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            $"""
             SELECT {OperatorPersistence.IdColumn}
             FROM {OperatorPersistence.TableName}
             WHERE {OperatorPersistence.IdColumn} = @operator_id
             FOR UPDATE;
             """);
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "operator_id";
        parameter.Value = operatorIdentifier;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SetLocalLockTimeoutAsync(CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(
            $"SET LOCAL lock_timeout = '{LockCommandTimeoutSeconds}s';");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private DbCommand CreateCommand(string commandText)
    {
        DbCommand command = persistence.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = LockCommandTimeoutSeconds;
        command.Transaction = persistence.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Operator mutation locking requires a caller transaction.");
        return command;
    }

    private async Task RollbackAndClearAsync(IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        persistence.ChangeTracker.Clear();
    }

    private static bool IsConcurrencyFailure(PostgresException failure) =>
        failure.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected;

    private static OperatorMutationResponse ToResponse(Operator target) =>
        new(target.Id, ToStateToken(target.State), ToRoleToken(target.Role));

    private static string ToStateToken(OperatorState state) => state switch
    {
        OperatorState.Active => OperatorPersistence.ActiveStateToken,
        OperatorState.Disabled => OperatorPersistence.DisabledStateToken,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Operator state."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Operator role."),
    };
}

internal sealed class AtomicOperatorMutationSuccessCommitter(
    BankDbContext persistence,
    IAuditWriter auditWriter) : IOperatorMutationSuccessCommitter
{
    public async Task CommitAsync(
        Operator target,
        OperatorMutationKind operation,
        Guid actorIdentifier,
        OperatorRole actorRole,
        HttpContext httpContext,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(transaction);

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AuditWriteRequest successAudit = new(
            actorIdentifier,
            actorRole,
            OperatorMutationAudit.GetOperationIdentifier(operation),
            target.Id.ToString("D"),
            AuditResult.Success,
            FailureBusinessErrorCode: null,
            httpContext.TraceIdentifier);

        await auditWriter
            .AppendToCurrentTransactionAsync(successAudit, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
