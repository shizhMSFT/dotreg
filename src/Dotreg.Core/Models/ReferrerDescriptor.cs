namespace Dotreg.Core.Models;

/// <summary>
/// Represents a referrer descriptor in the OCI referrers index.
/// </summary>
public class ReferrerDescriptor
{
    /// <summary>
    /// The media type of the referenced artifact manifest.
    /// </summary>
    public required string MediaType { get; set; }

    /// <summary>
    /// The digest of the referenced artifact manifest.
    /// </summary>
    public required string Digest { get; set; }

    /// <summary>
    /// The size in bytes of the referenced artifact manifest.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// The artifact type (e.g., signature, SBOM, attestation).
    /// </summary>
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Optional annotations for the referrer.
    /// </summary>
    public Dictionary<string, string>? Annotations { get; set; }
}
