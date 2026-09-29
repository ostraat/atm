using Atm.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Atm.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/api/health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse { Status = "ok" });
}
