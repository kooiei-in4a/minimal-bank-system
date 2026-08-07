using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Application;

namespace MinimalBankSystem.Api.Controllers;

[ApiController]
[Route("__contract")]
public sealed class ContractProbeController(
    IApplicationClock clock,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet("success")]
    public ActionResult<ContractProbeResponse> Success()
    {
        if (!environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        return Ok(new ContractProbeResponse(clock.UtcNow));
    }

    [HttpGet("unmapped")]
    public ActionResult Unmapped()
    {
        if (!environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        throw new InvalidOperationException(
            "probe detail: password=sentinel-password jwt=sentinel-jwt signing-key=sentinel-signing-key "
            + "idempotency-key=sentinel-idempotency-key connection-string=sentinel-connection-string");
    }

    [HttpGet("validation")]
    public ActionResult<ContractProbeResponse> Validation([FromQuery] ContractProbeRequest request)
    {
        if (!environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        return Ok(new ContractProbeResponse(clock.UtcNow));
    }
}

public sealed record ContractProbeResponse(DateTimeOffset UtcNow);

public sealed class ContractProbeRequest
{
    [Required]
    public string? Value { get; init; }
}
