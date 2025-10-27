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
    private readonly ILogger<ManifestsController> _logger;

    public ManifestsController(IRegistryService registryService, ILogger<ManifestsController> logger)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        _logger.LogInformation("Retrieving manifest: {Repository}@{Reference}", name, reference);
        
        try
        {
            var manifest = await _registryService.GetManifestAsync(name, reference, cancellationToken);
            
            _logger.LogInformation(
                "Manifest retrieved successfully: {Repository}@{Reference}, Digest={Digest}, Size={Size}",
                name, reference, manifest.Digest, manifest.Size);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = manifest.Digest;
            Response.Headers["Content-Length"] = manifest.Size.ToString();

            return File(manifest.Content, manifest.MediaType);
        }
        catch (ManifestNotFoundException ex)
        {
            _logger.LogWarning(ex, "Manifest not found: {Repository}@{Reference}", name, reference);
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
        _logger.LogDebug("Checking manifest existence: {Repository}@{Reference}", name, reference);
        
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
