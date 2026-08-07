using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Application.Diagnostics;

namespace MinimalBankSystem.IntegrationTests.TestOnly;

// Test-only contract verification surface. This controller is never part of
// the Api assembly; it is wired into the pipeline exclusively by
// ContractTestWebApplicationFactory so production carries no business or
// diagnostic endpoints from this issue.
[ApiController]
[Route("__contract-test")]
public sealed class ContractVerificationController(CurrentTimeReader currentTimeReader) : ControllerBase
{
    [HttpGet("echo")]
    public IActionResult Echo() => Ok(new { currentTime = currentTimeReader.GetUtcNow() });

    [HttpGet("throw")]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "ASP.NET Core MVC action methods must be instance members.")]
    public IActionResult Throw() =>
        throw new InvalidOperationException("Contract test deliberate unmapped exception.");
}
