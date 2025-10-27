namespace Dotreg.Core.Services;

/// <summary>
/// Interface for storage operations in the registry.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Checks if an object exists at the specified key.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of an object in bytes.
    /// </summary>
    Task<long> GetSizeAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content type of an object.
    /// </summary>
    Task<string?> GetContentTypeAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an object as a byte array.
    /// </summary>
    Task<byte[]> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an object as a stream.
    /// </summary>
    Task<Stream> GetStreamAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes content to an object.
    /// </summary>
    Task PutAsync(string key, byte[] content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a stream to an object.
    /// </summary>
    Task PutStreamAsync(string key, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists objects with the specified prefix.
    /// </summary>
    Task<List<string>> ListAsync(string prefix, int? maxResults = null, string? startAfter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for an object.
    /// </summary>
    Task<Dictionary<string, string>> GetMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts an object with metadata.
    /// </summary>
    Task PutWithMetadataAsync(string key, byte[] content, string? contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default);
}
