using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;
using MinimalBankSystem.Api.Runtime;
using MinimalBankSystem.Application.Auditing;
using MinimalBankSystem.Application.Runtime;
using MinimalBankSystem.Domain.Auditing;
using MinimalBankSystem.Domain.Identity;
using MinimalBankSystem.Infrastructure.Persistence;
using MinimalBankSystem.Infrastructure.Persistence.Identity;
using Npgsql;

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
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] OperatorCreateRequest? request,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = GetCurrentActor();

        if (string.IsNullOrWhiteSpace(request?.LoginIdentifier) ||
            string.IsNullOrWhiteSpace(request?.Password))
        {
            return await RejectAsync(
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string loginIdentifier = request.LoginIdentifier.Trim();
        if (loginIdentifier.Length > Operator.UserNameMaxLength)
        {
            return await RejectAsync(
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        OperatorRole? role = ParseRoleToken(request.Role);
        if (role is null)
        {
            return await RejectAsync(
                    ApiErrorEnvelope.ValidationFailed,
                    StatusCodes.Status400BadRequest,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string normalizedLoginIdentifier = loginIdentifier.ToUpperInvariant();
        bool alreadyRegistered = await persistence.Operators
            .AsNoTracking()
            .AnyAsync(
                operatorEntity => operatorEntity.NormalizedUserName == normalizedLoginIdentifier,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyRegistered)
        {
            return await RejectAsync(
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Operator newOperator = OperatorFactory.Create(
            loginIdentifier,
            request.Password,
            role.Value,
            applicationTime.GetUtcNow(),
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            await HttpContext.RequestServices
                .GetRequiredService<IOperatorCreateSuccessCommitter>()
                .CommitAsync(newOperator, actor.Identifier, actor.Role, HttpContext, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (IsLoginIdentifierUniqueViolation(failure))
        {
            // A concurrent request registering the same normalized login identifier won the race
            // after the pre-check above. The failed insert left no Operator row behind.
            return await RejectAsync(
                    ApiErrorEnvelope.OperatorLoginIdentifierAlreadyRegistered,
                    StatusCodes.Status409Conflict,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Created(
            $"/operators/{newOperator.Id:D}",
            new OperatorCreateResponse(
                newOperator.Id,
                ToStateToken(newOperator.State),
                ToRoleToken(newOperator.Role)));
    }

    private CurrentOperatorSnapshot GetCurrentActor() =>
        HttpContext.RequestServices.GetRequiredService<CurrentOperatorRequestContext>().CurrentOperator
        ?? throw new InvalidOperationException(
            "An authorized Operator create requires a current Product-Audit actor.");

    private async Task<IActionResult> RejectAsync(
        ApiErrorEnvelope error,
        int statusCode,
        CurrentOperatorSnapshot actor,
        CancellationToken cancellationToken)
    {
        AuditWriteRequest rejectionAudit = new(
            actor.Identifier,
            actor.Role,
            OperatorCreateAudit.OperationIdentifier,
            OperatorCreateAudit.TargetIdentifier,
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

    private static bool IsLoginIdentifierUniqueViolation(DbUpdateException failure) =>
        failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresFailure &&
        string.Equals(
            postgresFailure.ConstraintName,
            OperatorPersistence.NormalizedUserNameIndex,
            StringComparison.Ordinal);

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
            "Unknown Operator state cannot be exposed by the create API."),
    };

    private static string ToRoleToken(OperatorRole role) => role switch
    {
        OperatorRole.Administrator => OperatorPersistence.AdministratorRoleToken,
        OperatorRole.Teller => OperatorPersistence.TellerRoleToken,
        OperatorRole.Viewer => OperatorPersistence.ViewerRoleToken,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "Unknown Operator role cannot be exposed by the create API."),
    };
}

public sealed record OperatorCreateRequest(string? LoginIdentifier, string? Password, string? Role);

/// <summary>
/// Deliberately closed Operator create success projection. Credential, security and
/// authorization-state fields remain unavailable to MVC serialization because they are not part
/// of this type.
/// </summary>
public sealed record OperatorCreateResponse(Guid OperatorIdentifier, string State, string Role);
