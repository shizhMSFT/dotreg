using Dotreg.Core.Exceptions;
using Dotreg.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dotreg.Api.Controllers;

/// <summary>
/// OCI blob endpoints for retrieving layers and configs
/// GET /v2/{name}/blobs/{digest} - get blob by digest
/// HEAD /v2/{name}/blobs/{digest} - check if blob exists
/// Supports Range requests for partial downloads
/// </summary>
[ApiController]
[Route("v2/{name}/blobs")]
public class BlobsController : ControllerBase
{
    private readonly IRegistryService _registryService;
    private readonly ILogger<BlobsController> _logger;

    public BlobsController(IRegistryService registryService, ILogger<BlobsController> logger)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get a blob by digest
    /// </summary>
    /// <param name="name">Repository name (can contain slashes)</param>
    /// <param name="digest">Blob digest (sha256:...)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{digest}")]
    public async Task<IActionResult> GetBlob(string name, string digest, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving blob: {Repository}@{Digest}", name, digest);
        
        try
        {
            var blob = await _registryService.GetBlobAsync(name, digest, cancellationToken);
            
            _logger.LogInformation(
                "Blob retrieved successfully: {Repository}@{Digest}, Size={Size}",
                name, digest, blob.Size);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = blob.Digest;
            Response.Headers["Content-Length"] = blob.Size.ToString();

            // Return stream with Range support
            return File(blob.Content, blob.MediaType, enableRangeProcessing: true);
        }
        catch (BlobNotFoundException ex)
        {
            _logger.LogWarning(ex, "Blob not found: {Repository}@{Digest}", name, digest);
            return NotFound(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.BlobUnknown,
                        Message = ex.Message,
                        Detail = $"Blob not found: {name}@{digest}"
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
        catch (DigestMismatchException ex)
        {
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.DigestInvalid,
                        Message = ex.Message
                    }
                }
            });
        }
    }

    /// <summary>
    /// Check if a blob exists (HEAD request)
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="digest">Blob digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpHead("{digest}")]
    public async Task<IActionResult> HeadBlob(string name, string digest, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking blob existence: {Repository}@{Digest}", name, digest);
        
        try
        {
            var blob = await _registryService.GetBlobAsync(name, digest, cancellationToken);

            // Set OCI headers
            Response.Headers["Docker-Content-Digest"] = blob.Digest;
            Response.Headers["Content-Length"] = blob.Size.ToString();

            // Dispose the stream since we don't need the content
            await blob.Content.DisposeAsync();

            return Ok();
        }
        catch (BlobNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidNameException)
        {
            return BadRequest();
        }
        catch (DigestMismatchException)
        {
            return BadRequest();
        }
    }

    /// <summary>
    /// Delete a blob (DELETE request)
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="digest">Blob digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{digest}")]
    public async Task<IActionResult> DeleteBlob(string name, string digest, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting blob: {Repository}@{Digest}", name, digest);

        try
        {
            await _registryService.DeleteBlobAsync(name, digest, cancellationToken);

            _logger.LogInformation(
                "Blob deleted successfully: {Repository}@{Digest}",
                name, digest);

            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deletion disabled for blob: {Repository}@{Digest}", name, digest);
            return new ObjectResult(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.Unsupported,
                        Message = ex.Message,
                        Detail = "Blob deletion is not enabled"
                    }
                }
            })
            {
                StatusCode = 405 // Method Not Allowed
            };
        }
        catch (BlobNotFoundException ex)
        {
            _logger.LogWarning(ex, "Blob not found for deletion: {Repository}@{Digest}", name, digest);
            return NotFound(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.BlobUnknown,
                        Message = ex.Message
                    }
                }
            });
        }
        catch (InvalidNameException ex)
        {
            _logger.LogWarning(ex, "Invalid digest in blob deletion: {Repository}@{Digest}", name, digest);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob: {Repository}@{Digest}", name, digest);
            throw;
        }
    }
}
