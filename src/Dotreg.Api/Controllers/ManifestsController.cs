using Dotreg.Core.Exceptions;
using Dotreg.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dotreg.Api.Controllers;

/// <summary>
/// OCI manifest endpoints for retrieving container manifests
/// GET /v2/{name}/manifests/{reference} - get manifest by tag or digest
/// HEAD /v2/{name}/manifests/{reference} - check if manifest exists
/// </summary>
[ApiController]
[Route("v2/{name}/manifests")]
public class ManifestsController : ControllerBase
{
    private readonly IRegistryService _registryService;

    public ManifestsController(IRegistryService registryService)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
    }

    /// <summary>
    /// Get a manifest by tag or digest
    /// </summary>
    /// <param name="name">Repository name (can contain slashes)</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{**reference}")]
    public async Task<IActionResult> GetManifest(string name, string reference, CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await _registryService.GetManifestAsync(name, reference, cancellationToken);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = manifest.Digest;
            Response.Headers["Content-Length"] = manifest.Size.ToString();

            return File(manifest.Content, manifest.MediaType);
        }
        catch (ManifestNotFoundException ex)
        {
            return NotFound(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.ManifestUnknown,
                        Message = ex.Message,
                        Detail = $"Manifest not found: {name}@{reference}"
                    }
                }
            });
        }
        catch (InvalidNameException ex)
        {
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.NameInvalid,
                        Message = ex.Message
                    }
                }
            });
        }
    }

    /// <summary>
    /// Check if a manifest exists (HEAD request)
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpHead("{**reference}")]
    public async Task<IActionResult> HeadManifest(string name, string reference, CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await _registryService.GetManifestAsync(name, reference, cancellationToken);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = manifest.Digest;
            Response.Headers["Content-Length"] = manifest.Size.ToString();
            Response.Headers["Content-Type"] = manifest.MediaType;

            return Ok();
        }
        catch (ManifestNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidNameException)
        {
            return BadRequest();
        }
    }
}
