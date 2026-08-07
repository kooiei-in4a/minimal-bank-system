using Microsoft.AspNetCore.Mvc;
using MinimalBankSystem.Api.Models;
using MinimalBankSystem.Domain.Errors;

namespace MinimalBankSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed partial class ContractVerificationController : ControllerBase
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContractVerificationController> _logger;

    public ContractVerificationController(TimeProvider timeProvider, ILogger<ContractVerificationController> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet("time")]
    public IActionResult GetTime()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        LogTimeEndpointCalled(now);
        return Ok(new { currentTime = now });
    }

    [HttpGet("correlation")]
    public IActionResult GetCorrelation()
    {
        string correlationId = HttpContext.Items["CorrelationId"] as string ?? "unknown";
        LogCorrelationEndpointCalled(correlationId);
        return Ok(new { correlationId });
    }

    [HttpGet("error/validation")]
    public IActionResult TriggerValidationError()
    {
        return BadRequest(new ErrorResponse(DomainErrors.Common.ValidationError, "Test validation error"));
    }

    [HttpGet("error/unmapped")]
    public IActionResult TriggerUnmappedException()
    {
        throw new InvalidOperationException("This is a test unmapped exception with sensitive details");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Time endpoint called. CurrentTime={CurrentTime}")]
    private partial void LogTimeEndpointCalled(DateTimeOffset currentTime);

    [LoggerMessage(Level = LogLevel.Information, Message = "Correlation endpoint called. CorrelationId={CorrelationId}")]
    private partial void LogCorrelationEndpointCalled(string correlationId);
}
