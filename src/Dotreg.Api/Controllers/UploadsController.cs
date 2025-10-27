using Dotreg.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Dotreg.Api.Controllers;

/// <summary>
/// Handles blob upload operations.
/// </summary>
[ApiController]
[Route("v2/{name}/blobs/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IRegistryService _registryService;
    private readonly IUploadSessionManager _uploadSessionManager;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(
        IRegistryService registryService,
        IUploadSessionManager uploadSessionManager,
        ILogger<UploadsController> logger)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _uploadSessionManager = uploadSessionManager ?? throw new ArgumentNullException(nameof(uploadSessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initiates a blob upload session.
    /// POST /v2/{name}/blobs/uploads/
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> InitiateUpload(
        string name,
        [FromQuery] string? digest = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating upload for repository: {Repository}", name);

        var sessionId = await _uploadSessionManager.CreateSessionAsync(name, cancellationToken);

        var location = $"{Request.Scheme}://{Request.Host}/v2/{name}/blobs/uploads/{sessionId}";

        _logger.LogInformation(
            "Upload session created for repository: {Repository}, SessionId: {SessionId}",
            name,
            sessionId);

        return Accepted(location, new { uuid = sessionId });
    }

    /// <summary>
    /// Uploads a chunk of data to an existing upload session.
    /// PATCH /v2/{name}/blobs/uploads/{uuid}
    /// </summary>
    [HttpPatch("{uuid}")]
    public async Task<IActionResult> UploadChunk(
        string name,
        Guid uuid,
        CancellationToken cancellationToken = default)
    {
        var contentRange = Request.Headers["Content-Range"].ToString();
        var contentLength = Request.ContentLength ?? 0;

        if (contentLength == 0)
        {
            _logger.LogWarning(
                "Empty chunk upload attempt for repository: {Repository}, SessionId: {SessionId}",
                name,
                uuid);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.BlobUploadInvalid,
                        Message = "Content-Length is required and must be greater than 0"
                    }
                }
            });
        }

        _logger.LogInformation(
            "Uploading chunk for repository: {Repository}, SessionId: {SessionId}, Size: {Size}, Range: {Range}",
            name,
            uuid,
            contentLength,
            contentRange);

        // Parse Content-Range header (e.g., "0-4" means bytes 0 through 4)
        long startByte = 0;
        long endByte = -1;
        if (!string.IsNullOrEmpty(contentRange))
        {
            var parts = contentRange.Split('-');
            if (parts.Length == 2 && long.TryParse(parts[0], out var start) && long.TryParse(parts[1], out var end))
            {
                startByte = start;
                endByte = end;

                // Validate that the range matches content length
                var expectedLength = endByte - startByte + 1;
                if (expectedLength != contentLength)
                {
                    _logger.LogWarning(
                        "Content-Range mismatch for repository: {Repository}, SessionId: {SessionId}, Expected: {Expected}, Actual: {Actual}",
                        name,
                        uuid,
                        expectedLength,
                        contentLength);
                    return BadRequest(new Models.OciErrorResponse
                    {
                        Errors = new List<Models.ErrorDetail>
                        {
                            new Models.ErrorDetail
                            {
                                Code = Models.OciErrorCodes.BlobUploadInvalid,
                                Message = $"Content-Range ({contentRange}) does not match Content-Length ({contentLength})"
                            }
                        }
                    });
                }
            }
        }

        try
        {
            var uploadedBytes = await _uploadSessionManager.UploadChunkAsync(
                uuid,
                Request.Body,
                startByte,
                contentLength,
                cancellationToken);

            var location = $"{Request.Scheme}://{Request.Host}/v2/{name}/blobs/uploads/{uuid}";

            Response.Headers["Location"] = location;
            Response.Headers["Range"] = $"0-{uploadedBytes - 1}";
            Response.Headers["Docker-Upload-UUID"] = uuid.ToString();

            _logger.LogInformation(
                "Chunk uploaded successfully for repository: {Repository}, SessionId: {SessionId}, TotalUploaded: {TotalUploaded}",
                name,
                uuid,
                uploadedBytes);

            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upload chunk for repository: {Repository}, SessionId: {SessionId}",
                name,
                uuid);
            throw;
        }
    }

    /// <summary>
    /// Completes an upload session and validates the digest.
    /// PUT /v2/{name}/blobs/uploads/{uuid}?digest={digest}
    /// </summary>
    [HttpPut("{uuid}")]
    public async Task<IActionResult> CompleteUpload(
        string name,
        Guid uuid,
        [FromQuery(Name = "digest")] string digest,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(digest))
        {
            _logger.LogWarning(
                "Complete upload called without digest for repository: {Repository}, SessionId: {SessionId}",
                name,
                uuid);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.DigestInvalid,
                        Message = "Digest query parameter is required"
                    }
                }
            });
        }

        _logger.LogInformation(
            "Completing upload for repository: {Repository}, SessionId: {SessionId}, Digest: {Digest}",
            name,
            uuid,
            digest);

        Stream? finalChunk = null;
        if (Request.ContentLength > 0)
        {
            finalChunk = Request.Body;
        }

        try
        {
            var validatedDigest = await _uploadSessionManager.CompleteUploadAsync(
                uuid,
                digest,
                finalChunk,
                cancellationToken);

            var location = $"{Request.Scheme}://{Request.Host}/v2/{name}/blobs/{validatedDigest}";

            Response.Headers["Location"] = location;
            Response.Headers["Docker-Content-Digest"] = validatedDigest;

            _logger.LogInformation(
                "Upload completed successfully for repository: {Repository}, SessionId: {SessionId}, Digest: {Digest}",
                name,
                uuid,
                validatedDigest);

            return Created(location, null);
        }
        catch (Core.Exceptions.DigestMismatchException ex)
        {
            _logger.LogWarning(ex,
                "Digest mismatch for repository: {Repository}, SessionId: {SessionId}, Expected: {Expected}, Actual: {Actual}",
                name,
                uuid,
                digest,
                ex.Message);
            return BadRequest(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.DigestInvalid,
                        Message = ex.Message,
                        Detail = "The uploaded content does not match the provided digest"
                    }
                }
            });
        }
        catch (Core.Exceptions.UploadSessionNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Upload session not found for repository: {Repository}, SessionId: {SessionId}",
                name,
                uuid);
            return NotFound(new Models.OciErrorResponse
            {
                Errors = new List<Models.ErrorDetail>
                {
                    new Models.ErrorDetail
                    {
                        Code = Models.OciErrorCodes.BlobUploadUnknown,
                        Message = $"Upload session {uuid} not found"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to complete upload for repository: {Repository}, SessionId: {SessionId}",
                name,
                uuid);
            throw;
        }
    }

    /// <summary>
    /// Gets the upload progress for a session.
    /// GET /v2/{name}/blobs/uploads/{uuid}
    /// </summary>
    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetUploadProgress(
        string name,
        Guid uuid,
        CancellationToken cancellationToken = default)
    {
        var uploadedBytes = await _uploadSessionManager.GetUploadProgressAsync(uuid, cancellationToken);

        var location = $"{Request.Scheme}://{Request.Host}/v2/{name}/blobs/uploads/{uuid}";

        Response.Headers["Location"] = location;
        Response.Headers["Range"] = $"0-{uploadedBytes - 1}";
        Response.Headers["Docker-Upload-UUID"] = uuid.ToString();

        return NoContent();
    }

    /// <summary>
    /// Cancels an upload session.
    /// DELETE /v2/{name}/blobs/uploads/{uuid}
    /// </summary>
    [HttpDelete("{uuid}")]
    public async Task<IActionResult> CancelUpload(
        string name,
        Guid uuid,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Cancelling upload for repository: {Repository}, SessionId: {SessionId}",
            name,
            uuid);

        await _uploadSessionManager.CancelUploadAsync(uuid, cancellationToken);

        return NoContent();
    }
}
