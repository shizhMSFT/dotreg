namespace Dotreg.Core.Models;

/// <summary>
/// Represents an OCI blob (layer or config)
/// </summary>
public class Blob
{
    /// <summary>
    /// The digest of the blob (e.g., sha256:abc123...)
    /// </summary>
    public required string Digest { get; init; }

    /// <summary>
    /// The size of the blob in bytes
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// The content stream of the blob
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// The media type of the blob (typically application/octet-stream)
    /// </summary>
    public string MediaType { get; init; } = "application/octet-stream";
}
