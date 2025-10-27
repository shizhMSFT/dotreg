using System.Text.Json.Serialization;

namespace Dotreg.Api.Models;

/// <summary>
/// Represents an OCI Image Index as returned by the referrers API.
/// </summary>
public class ImageIndex
{
    /// <summary>
    /// The schema version (always 2 for OCI).
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// The media type of this index.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "application/vnd.oci.image.index.v1+json";

    /// <summary>
    /// The list of manifest descriptors.
    /// </summary>
    [JsonPropertyName("manifests")]
    public List<ManifestDescriptor> Manifests { get; set; } = new();
}

/// <summary>
/// Represents a manifest descriptor within an image index.
/// </summary>
public class ManifestDescriptor
{
    /// <summary>
    /// The media type of the manifest.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; set; }

    /// <summary>
    /// The digest of the manifest.
    /// </summary>
    [JsonPropertyName("digest")]
    public required string Digest { get; set; }

    /// <summary>
    /// The size in bytes of the manifest.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// The artifact type (optional, for referrers).
    /// </summary>
    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Optional annotations.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Annotations { get; set; }
}
