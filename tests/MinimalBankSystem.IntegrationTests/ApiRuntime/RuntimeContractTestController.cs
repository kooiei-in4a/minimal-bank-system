using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MinimalBankSystem.Api.Runtime;

namespace MinimalBankSystem.IntegrationTests.ApiRuntime;

/// <summary>
/// Representative non-business endpoints that exercise the common API runtime contract.
/// </summary>
/// <remarks>
/// The controller lives in the test assembly and is mounted only by
/// <see cref="ApiRuntimeTestServer"/>. It is never part of the production API surface, so the
/// contract can be verified without introducing a business feature or a diagnostic endpoint.
/// </remarks>
[ApiController]
[Route(RouteBase)]
public sealed class RuntimeContractTestController(
    TimeProvider timeProvider,
    ILogger<RuntimeContractTestController> logger)
    : ControllerBase
{
    public const string RouteBase = "__runtime-contract";

    /// <summary>
    /// Internal detail that must never appear in an API response.
    /// </summary>
    public const string UnmappedExceptionDetail = "unmapped-exception-detail-must-not-reach-the-caller";

    [HttpGet("time")]
    public IActionResult GetTime() => Ok(new { utcNow = timeProvider.GetUtcNow() });

    [HttpGet("correlation")]
    public IActionResult GetCorrelation()
    {
        logger.RepresentativeEndpointExecuted();

        return Ok(new
        {
            correlationId = HttpContext.GetCorrelationId(),

            // Echoed so a test can prove the untrusted value really reached the application and was
            // rejected by the policy, rather than being dropped by the transport.
            suppliedCorrelationIds = Request.Headers[CorrelationIdPolicy.HeaderName].ToArray(),
        });
    }

    [HttpPost("failure/unmapped")]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "MVC only routes to instance action methods.")]
    public IActionResult FailUnmapped([FromBody] RuntimeContractTestPayload? payload)
    {
        // The payload is bound but deliberately never inspected or logged: caller supplied data must
        // not reach technical log output through the runtime contract.
        _ = payload;

        throw new InvalidOperationException(UnmappedExceptionDetail);
    }

    [HttpGet("failure/mapped")]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "MVC only routes to instance action methods.")]
    public IActionResult FailMapped() => throw new RuntimeContractTestException();

    [HttpPost("validated")]
    public IActionResult Validated([FromBody] ValidatedPayload payload) => Ok(new { name = payload.Name });

    [HttpGet("client-error")]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "MVC only routes to instance action methods.")]
    public IActionResult ClientError() => NotFound();
}

/// <summary>
/// Request body used to prove that bound caller data is not written to technical logs.
/// </summary>
public sealed class RuntimeContractTestPayload
{
    public string? Password { get; init; }

    public string? SigningKey { get; init; }

    public string? ConnectionString { get; init; }
}

/// <summary>
/// Request body used to prove that framework input validation returns the common error envelope.
/// </summary>
public sealed class ValidatedPayload
{
    [Required]
    [MinLength(1)]
    public string? Name { get; init; }
}
