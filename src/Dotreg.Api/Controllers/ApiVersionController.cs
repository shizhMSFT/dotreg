using Microsoft.AspNetCore.Mvc;

namespace Dotreg.Api.Controllers;

/// <summary>
/// OCI Distribution API version check endpoint
/// GET /v2/ returns 200 OK to indicate registry is available
/// </summary>
[ApiController]
[Route("v2")]
public class ApiVersionController : ControllerBase
{
    /// <summary>
    /// Version check endpoint per OCI Distribution Spec
    /// </summary>
    /// <returns>200 OK with Docker-Distribution-Api-Version header</returns>
    [HttpGet("")]
    public Task<IActionResult> GetApiVersion()
    {
        Response.Headers["Docker-Distribution-Api-Version"] = "registry/2.0";
        return Task.FromResult<IActionResult>(Ok());
    }
}
