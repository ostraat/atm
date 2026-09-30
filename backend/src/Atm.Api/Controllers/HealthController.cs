using Atm.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Atm.Api.Controllers;

/// <summary>Process health checks.</summary>
[ApiController]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Return service liveness.</summary>
    /// <returns>Status payload with <c>status: ok</c> when the process is up.</returns>
    /// <response code="200">Process is up.</response>
    [HttpGet("/api/health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse { Status = "ok" });
}
