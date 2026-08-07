using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MinimalBankSystem.IntegrationTests.TestInfrastructure;

[ApiController]
[Route("test")]
public sealed class RuntimeContractTestController : ControllerBase
{
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] TestRequest request)
    {
        return Ok(new { request.Name });
    }

    public sealed class TestRequest
    {
        [Required]
        public string? Name { get; set; }
    }
}
