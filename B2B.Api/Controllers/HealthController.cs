using Microsoft.AspNetCore.Mvc;
using B2B.Api.Contracts;
using B2B.Api.Infrastructure;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    /// <summary>Health check.</summary>
    /// <remarks>
    /// Example request:
    /// <code>
    /// GET /api/v1/health
    /// </code>
    /// </remarks>
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> Get()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            status = "ok",
            timeUtc = DateTime.UtcNow
        }, HttpContext.TraceId()));
    }
}

