using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MinimalBankSystem.Api.Authorization;

namespace MinimalBankSystem.Api.OperatorCreate;

[ApiController]
[Route("operators")]
public sealed class OperatorCreateController(IOperatorCreateExecutor executor) : ControllerBase
{
    [Authorize(Policy = CurrentOperatorPolicyNames.Administrator)]
    [OperatorCreateAuthorizationAuditContext]
    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateOperatorRequest? request,
        CancellationToken cancellationToken)
    {
        CurrentOperatorSnapshot actor = HttpContext.RequestServices
                .GetRequiredService<CurrentOperatorRequestContext>()
                .CurrentOperator
            ?? throw new InvalidOperationException(
                "An authorized Operator create requires a current Product-Audit actor.");

        return executor.ExecuteAsync(
            request ?? new CreateOperatorRequest(null, null, null),
            actor.Identifier,
            actor.Role,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }
}

