using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

namespace MinimalBankSystem.Api.OperatorCreate;

[ApiController]
[Route("operators")]
public sealed class OperatorCreateController(
    BankDbContext persistence,
    IAuditWriter auditWriter,
    ApplicationTime applicationTime) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorCreateAuthorizationAuditContext]
    [HttpPost]
    public async Task<IActionResult> Create(
        OperatorCreateRequest? request,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();

        if (request is null ||
            string.IsNullOrWhiteSpace(request.LoginIdentifier) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            !TryParseRole(request.Role, out OperatorRole role))
        {
            return await RejectAsync(
                    actor,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string normalizedLoginIdentifier = request.LoginIdentifier
            .Trim()
            .ToUpperInvariant();

        if (normalizedLoginIdentifier.Length > Operator.UserNameMaxLength)
        {
            return await RejectAsync(
                    actor,
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        bool duplicate = await persistence.Operators
            .AsNoTracking()
            .AnyAsync(
                operatorEntity => operatorEntity.NormalizedUserName == normalizedLoginIdentifier,
                cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            return await RejectAsync(
                    actor,
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Operator created = OperatorFactory.Create(
            request.LoginIdentifier,
            request.Password,
            role,
            applicationTime.GetUtcNow(),
            Guid.NewGuid().ToString("N"));

        try
        {
            await PersistWithRequiredSuccessAuditAsync(
                    actor,
                    created,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (OperatorPersistence.IsNormalizedUserNameUniqueViolation(exception))
        {
            // A concurrent creator can win between the pre-check and the unique index. The
            // caller transaction has already been rolled back and the tracker is cleared before
            // the rejection Audit starts its independent short transaction.
            return await RejectAsync(
                    actor,
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        OperatorCreateResponse response = new(
            created.Id,
            "active",
            ToRoleToken(created.Role));

        // This is intentionally after transaction commit. No result factory or response payload
        // is constructed on the success path until both Operator and required Audit are durable.
        return Created($"/operators/{created.Id:D}", response);
    }

    private async Task PersistWithRequiredSuccessAuditAsync(
        CurrentOperatorSnapshot actor,
        Operator created,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await persistence.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        bool auditAppendAttempted = false;
        bool auditAppendCompleted = false;

        try
        {
            persistence.Operators.Add(created);
            await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AuditWriteRequest audit = new(
                actor.Identifier,
                actor.Role,
                OperatorCreateAudit.OperationIdentifier,
                created.Id.ToString("D"),
                AuditResult.Success,
                FailureBusinessErrorCode: null,
                HttpContext.TraceIdentifier);

            auditAppendAttempted = true;
            await auditWriter
                .AppendToCurrentTransactionAsync(audit, cancellationToken)
                .ConfigureAwait(false);
            auditAppendCompleted = true;

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // PostgreSqlAuditWriter rolls back a required Audit failure before propagating it.
            // For failures before the Audit primitive, or after a successful append but before
            // commit, the caller owns the explicit rollback. Disposal remains the fail-closed
            // fallback for a test-only writer that violates the primitive's rollback contract.
            if (!auditAppendAttempted || auditAppendCompleted)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            persistence.ChangeTracker.Clear();
            throw;
        }
    }

    private Task<IActionResult> RejectAsync(
        CurrentOperatorSnapshot actor,
        ApiErrorEnvelope error,
        int statusCode,
        CancellationToken cancellationToken)
    {
        AuditWriteRequest audit = new(
            actor.Identifier,
            actor.Role,
            OperatorCreateAudit.OperationIdentifier,
            OperatorCreateAudit.CollectionTargetIdentifier,
            AuditResult.Failure,
            error.Code,
            HttpContext.TraceIdentifier);

        return auditWriter.AppendInSeparateTransactionBeforeResultAsync(
            audit,
            _ => Task.FromResult<IActionResult>(StatusCode(statusCode, error)),
            cancellationToken);
    }

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator create requires a current Product-Audit actor.");

    private static bool TryParseRole(string? token, out OperatorRole role)
    {
        role = token switch
        {
            "administrator" => OperatorRole.Administrator,
            "teller" => OperatorRole.Teller,
            "viewer" => OperatorRole.Viewer,
            _ => OperatorRole.Unspecified,
        };

        return role != OperatorRole.Unspecified;
    }

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => "administrator",
        OperatorRole.Teller => "teller",
        OperatorRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role.ToString(),
            "Unknown Operator role."),
    };
}

public sealed record OperatorCreateRequest(
    string? LoginIdentifier,
    string? Password,
    string? Role);

public sealed record OperatorCreateResponse(
    Guid OperatorIdentifier,
    string State,
    string Role);
