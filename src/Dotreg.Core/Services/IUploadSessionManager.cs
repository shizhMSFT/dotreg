using Dotreg.Core.Models;

namespace Dotreg.Core.Services;

/// <summary>
/// Manages upload sessions for chunked blob uploads.
/// </summary>
public interface IUploadSessionManager
{
    /// <summary>
    /// Creates a new upload session.
    /// </summary>
    /// <param name="repository">Repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session ID (UUID).</returns>
    Task<Guid> CreateSessionAsync(string repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing upload session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload session details.</returns>
    Task<UploadSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a chunk of data to an existing session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="content">Content stream.</param>
    /// <param name="startByte">Starting byte position.</param>
    /// <param name="length">Length of chunk in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total bytes uploaded so far.</returns>
    Task<long> UploadChunkAsync(
        Guid sessionId,
        Stream content,
        long startByte,
        long length,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an upload session and validates the digest.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="digest">Expected digest.</param>
    /// <param name="finalChunk">Optional final chunk of data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validated digest.</returns>
    Task<string> CompleteUploadAsync(
        Guid sessionId,
        string digest,
        Stream? finalChunk = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the upload progress for a session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total bytes uploaded.</returns>
    Task<long> GetUploadProgressAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an upload session and cleans up resources.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelUploadAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
