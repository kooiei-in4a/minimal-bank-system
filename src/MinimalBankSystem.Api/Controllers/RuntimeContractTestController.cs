using MinimalBankSystem.Application.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace MinimalBankSystem.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class RuntimeContractTestController(
    IHostEnvironment environment,
    ApplicationClock applicationClock) : ControllerBase
{
    public const string TestExceptionMessageHeaderName = "X-Test-Exception-Message";

    [HttpGet("/_test/runtime/unmapped-exception")]
    public IActionResult ThrowUnmappedException()
    {
        if (!environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        StringValues suppliedValues = Request.Headers[TestExceptionMessageHeaderName];
        string exceptionMessage = suppliedValues.Count == 1
            ? suppliedValues[0]!
            : "Runtime contract test exception.";

        throw new InvalidOperationException(exceptionMessage);
    }

    [HttpGet("/_test/runtime/utc-now")]
    public ActionResult<RuntimeTimeResponse> GetUtcNow()
    {
        if (!environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        return Ok(new RuntimeTimeResponse(applicationClock.GetUtcNow()));
    }

    public sealed record RuntimeTimeResponse(DateTimeOffset UtcNow);
}
