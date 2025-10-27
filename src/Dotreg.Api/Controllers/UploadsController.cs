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

        _logger.LogInformation(
            "Uploading chunk for repository: {Repository}, SessionId: {SessionId}, Size: {Size}",
            name,
            uuid,
            contentLength);

        // Parse Content-Range header (e.g., "0-4" means bytes 0 through 4)
        long startByte = 0;
        if (!string.IsNullOrEmpty(contentRange))
        {
            var parts = contentRange.Split('-');
            if (parts.Length == 2 && long.TryParse(parts[0], out var start))
            {
                startByte = start;
            }
        }

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

        return Accepted();
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

        var validatedDigest = await _uploadSessionManager.CompleteUploadAsync(
            uuid,
            digest,
            finalChunk,
            cancellationToken);

        var location = $"{Request.Scheme}://{Request.Host}/v2/{name}/blobs/{validatedDigest}";

        Response.Headers["Location"] = location;
        Response.Headers["Docker-Content-Digest"] = validatedDigest;

        _logger.LogInformation(
            "Upload completed for repository: {Repository}, Digest: {Digest}",
            name,
            validatedDigest);

        return Created(location, null);
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
