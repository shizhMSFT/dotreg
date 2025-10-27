using Dotreg.Core.Models;

namespace Dotreg.Core.Services;

/// <summary>
/// Registry service for OCI Distribution API operations
/// Handles manifest and blob retrieval with validation
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// Gets a manifest by repository name and reference (tag or digest)
    /// </summary>
    /// <param name="name">Repository name (e.g., library/nginx)</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The manifest</returns>
    /// <exception cref="Exceptions.ManifestNotFoundException">Manifest not found</exception>
    /// <exception cref="Exceptions.InvalidNameException">Invalid repository or tag name</exception>
    Task<Manifest> GetManifestAsync(string name, string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a blob by repository name and digest
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="digest">Blob digest (must be sha256:...)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blob with stream</returns>
    /// <exception cref="Exceptions.BlobNotFoundException">Blob not found</exception>
    /// <exception cref="Exceptions.InvalidNameException">Invalid repository name</exception>
    /// <exception cref="Exceptions.DigestMismatchException">Invalid digest format</exception>
    Task<Blob> GetBlobAsync(string name, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a manifest exists
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if manifest exists, false otherwise</returns>
    Task<bool> CheckManifestExistsAsync(string name, string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a blob exists
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="digest">Blob digest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if blob exists, false otherwise</returns>
    Task<bool> CheckBlobExistsAsync(string name, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a manifest with the given reference (tag or digest)
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="reference">Tag name or digest</param>
    /// <param name="content">Manifest content bytes</param>
    /// <param name="contentType">Content type (e.g., application/vnd.oci.image.manifest.v1+json)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The calculated digest of the manifest</returns>
    Task<string> PutManifestAsync(string name, string reference, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tags for a repository
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="startAfter">Tag name to start after (for pagination)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tag names in lexical order</returns>
    Task<List<string>> ListTagsAsync(string name, int maxResults, string? startAfter, CancellationToken cancellationToken = default);

}
