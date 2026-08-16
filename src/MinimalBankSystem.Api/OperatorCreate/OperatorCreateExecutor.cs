using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;

namespace MinimalBankSystem.Api.OperatorCreate;

internal sealed class OperatorCreateExecutor(
    BankDbContext persistence,
    IAuditWriter auditWriter,
    ApplicationTime applicationTime) : IOperatorCreateExecutor
{
    public async Task<IActionResult> ExecuteAsync(
        CreateOperatorRequest request,
        Guid actorIdentifier,
        OperatorRole actorRole,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OperatorCreateContract.HasUsableCredential(request.LoginIdentifier)
            || !OperatorCreateContract.HasUsableCredential(request.Password)
            || request.LoginIdentifier!.Trim().Length > Operator.UserNameMaxLength)
        {
            return await RejectAsync(
                    actorIdentifier,
                    actorRole,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!OperatorCreateContract.TryParseRole(request.Role, out OperatorRole role))
        {
            return await RejectAsync(
                    actorIdentifier,
                    actorRole,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string loginIdentifier = request.LoginIdentifier.Trim();
        string normalizedUserName = loginIdentifier.ToUpperInvariant();
        if (await persistence.Operators
                .AsNoTracking()
                .AnyAsync(
                    operatorEntity => operatorEntity.NormalizedUserName == normalizedUserName,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return await RejectAsync(
                    actorIdentifier,
                    actorRole,
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Operator created = OperatorFactory.Create(
            loginIdentifier,
            request.Password!,
            role,
            applicationTime.GetUtcNow(),
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            persistence.Operators.Add(created);
            await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await auditWriter
                .AppendToCurrentTransactionAsync(
                    OperatorCreateContract.Success(
                        actorIdentifier,
                        actorRole,
                        created.Id,
                        correlationId),
                    cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException persistenceFailure)
            when (OperatorPersistence.IsNormalizedUserNameConflict(persistenceFailure))
        {
            await RollBackIfActiveAsync(transaction, persistence).ConfigureAwait(false);
            persistence.ChangeTracker.Clear();

            return await RejectAsync(
                    actorIdentifier,
                    actorRole,
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        OperatorCreateResponse projection = OperatorCreateContract.ToResponse(created);
        return new CreatedResult(
            $"/operators/{OperatorCreateContract.CanonicalOperatorTarget(created.Id)}",
            projection);
    }

    private async Task<IActionResult> RejectAsync(
        Guid actorIdentifier,
        OperatorRole actorRole,
        ApiErrorEnvelope envelope,
        int statusCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return await auditWriter
            .AppendInSeparateTransactionBeforeResultAsync(
                OperatorCreateContract.Rejection(
                    actorIdentifier,
                    actorRole,
                    envelope.Code,
                    correlationId),
                _ => Task.FromResult<IActionResult>(
                    new ObjectResult(envelope) { StatusCode = statusCode }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task RollBackIfActiveAsync(
        IDbContextTransaction transaction,
        BankDbContext persistence)
    {
        if (persistence.Database.CurrentTransaction is null)
        {
            return;
        }

        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
