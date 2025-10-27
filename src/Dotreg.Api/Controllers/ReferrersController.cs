using Dotreg.Api.Models;
using Dotreg.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dotreg.Api.Controllers;

/// <summary>
/// Controller for OCI referrers API operations
/// Implements GET /v2/{name}/referrers/{digest}
/// </summary>
[ApiController]
[Route("v2/{name}/referrers")]
public class ReferrersController : ControllerBase
{
    private readonly IRegistryService _registryService;
    private readonly ILogger<ReferrersController> _logger;

    public ReferrersController(IRegistryService registryService, ILogger<ReferrersController> logger)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get referrers list for a manifest
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="digest">Subject manifest digest</param>
    /// <param name="artifactType">Optional artifact type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCI Image Index containing referrers</returns>
    [HttpGet("{digest}")]
    [Produces("application/vnd.oci.image.index.v1+json")]
    public async Task<IActionResult> GetReferrers(
        string name,
        string digest,
        [FromQuery] string? artifactType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Getting referrers for {Repository} with subject {Digest}, artifactType filter: {ArtifactType}",
                name, digest, artifactType ?? "(none)");

            // Get referrers from service
            var referrers = await _registryService.GetReferrersAsync(name, digest, artifactType, cancellationToken);

            // Build OCI Image Index response
            var imageIndex = new ImageIndex
            {
                SchemaVersion = 2,
                MediaType = "application/vnd.oci.image.index.v1+json",
                Manifests = referrers.Select(r => new ManifestDescriptor
                {
                    MediaType = r.MediaType,
                    Digest = r.Digest,
                    Size = r.Size,
                    ArtifactType = r.ArtifactType,
                    Annotations = r.Annotations
                }).ToList()
            };

            // Set Content-Type header
            Response.Headers["Content-Type"] = "application/vnd.oci.image.index.v1+json";

            // Add OCI-Filters-Applied header if filtering was applied
            if (!string.IsNullOrEmpty(artifactType))
            {
                Response.Headers["OCI-Filters-Applied"] = "artifactType";
            }

            _logger.LogInformation(
                "Returning {Count} referrers for {Repository}/{Digest}",
                imageIndex.Manifests.Count, name, digest);

            // Serialize to JSON and return with exact OCI media type (no charset)
            var json = System.Text.Json.JsonSerializer.Serialize(imageIndex, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            
            return Content(json, "application/vnd.oci.image.index.v1+json");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid name or digest for referrers request: {Repository}/{Digest}",
                name, digest);

            return BadRequest(new OciErrorResponse
            {
                Errors = new List<ErrorDetail>
                {
                    new()
                    {
                        Code = "NAME_INVALID",
                        Message = "Invalid repository name or digest",
                        Detail = ex.Message
                    }
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("disabled"))
        {
            _logger.LogWarning(
                "Referrers API is disabled, returning 404 for {Repository}/{Digest}",
                name, digest);

            // When referrers API is disabled, return 404 per OCI spec (fallback to tag schema)
            return NotFound(new OciErrorResponse
            {
                Errors = new List<ErrorDetail>
                {
                    new()
                    {
                        Code = "UNSUPPORTED",
                        Message = "Referrers API is not enabled",
                        Detail = "The referrers API is disabled. Use tag schema (sha256-{digest}) as fallback."
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error getting referrers for {Repository}/{Digest}",
                name, digest);

            return StatusCode(500, new OciErrorResponse
            {
                Errors = new List<ErrorDetail>
                {
                    new()
                    {
                        Code = "UNKNOWN",
                        Message = "Internal server error",
                        Detail = ex.Message
                    }
                }
            });
        }
    }
}
