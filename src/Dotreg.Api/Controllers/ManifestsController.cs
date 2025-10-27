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
    private readonly IConfiguration _configuration;

    public ManifestsController(
        IRegistryService registryService,
        ILogger<ManifestsController> logger,
        IConfiguration configuration)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

    /// <summary>
    /// Upload a manifest (PUT request)
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{**reference}")]
    public async Task<IActionResult> PutManifest(string name, string reference, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uploading manifest: {Repository}@{Reference}", name, reference);

        // Check manifest size limit from configuration
        var maxManifestSize = _configuration.GetValue<long>("Registry:MaxManifestSizeBytes", 4194304); // 4MB default
        var contentLength = Request.ContentLength ?? 0;

        if (contentLength == 0)
        {
            _logger.LogWarning("Empty manifest upload attempt for repository: {Repository}@{Reference}", name, reference);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.ManifestInvalid,
                        Message = "Manifest content is required"
                    }
                }
            });
        }

        if (contentLength > maxManifestSize)
        {
            _logger.LogWarning(
                "Manifest too large for repository: {Repository}@{Reference}, Size: {Size}, MaxSize: {MaxSize}",
                name, reference, contentLength, maxManifestSize);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.SizeInvalid,
                        Message = $"Manifest size {contentLength} exceeds maximum allowed size {maxManifestSize}",
                        Detail = $"Maximum manifest size is {maxManifestSize} bytes (configured in Registry:MaxManifestSizeBytes)"
                    }
                }
            });
        }

        // Read manifest content from request body
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, cancellationToken);
        var content = ms.ToArray();

        var contentType = Request.ContentType ?? "application/vnd.oci.image.manifest.v1+json";

        try
        {
            var digest = await _registryService.PutManifestAsync(name, reference, content, contentType, cancellationToken);

            _logger.LogInformation(
                "Manifest uploaded successfully: {Repository}@{Reference}, Digest={Digest}, Size={Size}, ContentType={ContentType}",
                name, reference, digest, content.Length, contentType);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = digest;
            Response.Headers["Location"] = $"/v2/{name}/manifests/{digest}";

            return Created($"/v2/{name}/manifests/{digest}", null);
        }
        catch (InvalidNameException ex)
        {
            _logger.LogWarning(ex, "Invalid name in manifest upload: {Repository}@{Reference}", name, reference);
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
        catch (Core.Exceptions.DigestMismatchException ex)
        {
            _logger.LogWarning(ex, "Digest mismatch in manifest upload: {Repository}@{Reference}", name, reference);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.DigestInvalid,
                        Message = ex.Message,
                        Detail = "The manifest content does not match the provided digest reference"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload manifest: {Repository}@{Reference}", name, reference);
            throw;
        }
    }

}
