namespace Dotreg.Core.Models;

/// <summary>
/// Represents an upload session for chunked blob uploads.
/// </summary>
public class UploadSession
{
    /// <summary>
    /// Unique identifier for the upload session.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Repository name for the upload.
    /// </summary>
    public required string Repository { get; set; }

    /// <summary>
    /// Expected digest of the complete blob (optional, can be provided at completion).
    /// </summary>
    public string? Digest { get; set; }

    /// <summary>
    /// Total number of bytes uploaded so far.
    /// </summary>
    public long UploadedBytes { get; set; }

    /// <summary>
    /// Expected total size of the blob (optional).
    /// </summary>
    public long? TotalSize { get; set; }

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the session expires (typically 24 hours from creation).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// S3 multipart upload ID (if using S3 multipart uploads).
    /// </summary>
    public string? S3UploadId { get; set; }
}
