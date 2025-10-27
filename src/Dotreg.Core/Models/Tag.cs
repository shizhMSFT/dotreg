namespace Dotreg.Core.Models;

/// <summary>
/// Represents a tag pointing to a manifest digest.
/// </summary>
public class Tag
{
    /// <summary>
    /// Tag name (e.g., "latest", "v1.0.0").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digest of the manifest this tag points to.
    /// </summary>
    public required string Digest { get; set; }

    /// <summary>
    /// Timestamp when the tag was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
