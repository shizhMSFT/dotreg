namespace Dotreg.Api.Models;

/// <summary>
/// OCI tag list response format.
/// </summary>
public class TagList
{
    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the list of tags.
    /// </summary>
    public required List<string> Tags { get; set; }
}
