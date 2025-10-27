using Dotreg.Core.Exceptions;
using Dotreg.Core.Models;
using Dotreg.Core.Validation;
using System.Text.Json;

namespace Dotreg.Core.Services;

/// <summary>
/// Manages upload sessions for chunked blob uploads.
/// </summary>
public class UploadSessionManager : IUploadSessionManager
{
    private readonly IStorageService _storage;
    private static readonly TimeSpan SessionExpiration = TimeSpan.FromHours(24);

    public UploadSessionManager(IStorageService storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<Guid> CreateSessionAsync(string repository, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var metadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = repository,
            ["created-at"] = now.ToString("O"),
            ["expires-at"] = now.Add(SessionExpiration).ToString("O"),
            ["uploaded-bytes"] = "0"
        };

        // Store in global sessions index
        var key = $"uploads/_sessions/{sessionId}.json";
        var content = JsonSerializer.SerializeToUtf8Bytes(metadata);
        await _storage.PutAsync(key, content, "application/json", cancellationToken);

        return sessionId;
    }

    public async Task<UploadSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // We need to find the repository from the session ID
            // For now, we'll extract it from the metadata
            // In production, consider using a separate index
            var metadata = await GetSessionMetadataAsync(sessionId, cancellationToken);

            if (!metadata.TryGetValue("repository", out var repository))
            {
                throw new UploadSessionNotFoundException(sessionId, "Repository not found in session metadata");
            }

            return new UploadSession
            {
                SessionId = sessionId,
                Repository = repository,
                Digest = metadata.GetValueOrDefault("digest"),
                UploadedBytes = long.Parse(metadata.GetValueOrDefault("uploaded-bytes", "0")),
                TotalSize = metadata.ContainsKey("total-size") ? long.Parse(metadata["total-size"]) : null,
                CreatedAt = DateTime.Parse(metadata.GetValueOrDefault("created-at", DateTime.UtcNow.ToString("O"))),
                ExpiresAt = DateTime.Parse(metadata.GetValueOrDefault("expires-at", DateTime.UtcNow.Add(SessionExpiration).ToString("O"))),
                S3UploadId = metadata.GetValueOrDefault("s3-upload-id")
            };
        }
        catch (BlobNotFoundException ex)
        {
            throw new UploadSessionNotFoundException(sessionId, "Upload session not found", ex);
        }
    }

    public async Task<long> UploadChunkAsync(
        Guid sessionId,
        Stream content,
        long startByte,
        long length,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);

        await _storage.AppendToUploadAsync(session.Repository, sessionId.ToString(), content, startByte, length, cancellationToken);

        var newUploadedBytes = session.UploadedBytes + length;

        // Update session metadata in global index
        var metadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = session.Repository,
            ["created-at"] = session.CreatedAt.ToString("O"),
            ["expires-at"] = session.ExpiresAt.ToString("O"),
            ["uploaded-bytes"] = newUploadedBytes.ToString()
        };

        if (session.Digest != null)
        {
            metadata["digest"] = session.Digest;
        }

        var key = $"uploads/_sessions/{sessionId}.json";
        var metadataContent = JsonSerializer.SerializeToUtf8Bytes(metadata);
        await _storage.PutAsync(key, metadataContent, "application/json", cancellationToken);

        return newUploadedBytes;
    }

    public async Task<string> CompleteUploadAsync(
        Guid sessionId,
        string digest,
        Stream? finalChunk = null,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);

        // If there's a final chunk, append it first
        if (finalChunk != null)
        {
            // HTTP request streams don't support Length property, so we need to read into a memory stream
            using var memoryStream = new MemoryStream();
            await finalChunk.CopyToAsync(memoryStream, cancellationToken);
            var chunkSize = memoryStream.Length;
            
            if (chunkSize > 0)
            {
                memoryStream.Position = 0;
                await _storage.AppendToUploadAsync(
                    session.Repository,
                    sessionId.ToString(),
                    memoryStream,
                    session.UploadedBytes,
                    chunkSize,
                    cancellationToken);
            }
        }

        // Get the complete upload content
        var uploadContent = await _storage.GetUploadContentAsync(session.Repository, sessionId.ToString(), cancellationToken);

        // Validate digest
        var calculatedDigest = await DigestValidator.CalculateSha256Async(uploadContent, cancellationToken);
        if (calculatedDigest != digest)
        {
            throw new DigestMismatchException(digest, calculatedDigest);
        }

        // Reset stream position
        uploadContent.Position = 0;

        // Store the blob
        var metadata = new Dictionary<string, string>
        {
            ["uploaded-at"] = DateTime.UtcNow.ToString("O"),
            ["size"] = uploadContent.Length.ToString()
        };

        await _storage.StoreBlobAsync(session.Repository, digest, uploadContent, metadata, cancellationToken);

        // Clean up upload session
        await _storage.DeleteUploadSessionAsync(session.Repository, sessionId.ToString(), cancellationToken);

        return digest;
    }

    public async Task<long> GetUploadProgressAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var metadata = await GetSessionMetadataAsync(sessionId, cancellationToken);
        return long.Parse(metadata.GetValueOrDefault("uploaded-bytes", "0"));
    }

    public async Task CancelUploadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        await _storage.DeleteUploadSessionAsync(session.Repository, sessionId.ToString(), cancellationToken);
    }

    private async Task<Dictionary<string, string>> GetSessionMetadataAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // Store session metadata with a global sessions index approach
        // Key: uploads/_sessions/{sessionId}.json
        var key = $"uploads/_sessions/{sessionId}.json";
        
        try
        {
            var content = await _storage.GetAsync(key, cancellationToken);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
            return metadata ?? throw new UploadSessionNotFoundException(sessionId, "Session metadata deserialization failed");
        }
        catch (BlobNotFoundException ex)
        {
            throw new UploadSessionNotFoundException(sessionId, "Upload session not found", ex);
        }
    }
}
