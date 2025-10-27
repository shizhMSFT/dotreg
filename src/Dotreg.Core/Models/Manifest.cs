namespace Dotreg.Core.Models;

/// <summary>
/// Represents an OCI image manifest
/// </summary>
public class Manifest
{
    /// <summary>
    /// The digest of the manifest (e.g., sha256:abc123...)
    /// </summary>
    public required string Digest { get; init; }

    /// <summary>
    /// The media type of the manifest (e.g., application/vnd.oci.image.manifest.v1+json)
    /// </summary>
    public required string MediaType { get; init; }

    /// <summary>
    /// The raw JSON content of the manifest
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// The size of the manifest in bytes
    /// </summary>
    public long Size => Content.Length;

    /// <summary>
    /// Optional subject descriptor for referrers
    /// </summary>
    public string? Subject { get; init; }
}
