namespace Dotreg.Storage.S3;

/// <summary>
/// Builds S3 object keys for registry entities following OCI Distribution Spec storage patterns.
/// </summary>
public static class S3KeyBuilder
{
    /// <summary>
    /// Builds the S3 key for a manifest by digest.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="digest">The manifest digest (e.g., sha256:abc123...).</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildManifestKey(string repositoryName, string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        return $"repositories/{repositoryName}/manifests/{digest}";
    }

    /// <summary>
    /// Builds the S3 key for a blob by digest.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="digest">The blob digest (e.g., sha256:abc123...).</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildBlobKey(string repositoryName, string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        return $"repositories/{repositoryName}/blobs/{digest}";
    }

    /// <summary>
    /// Builds the S3 key for a tag.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="tag">The tag name.</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildTagKey(string repositoryName, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return $"repositories/{repositoryName}/tags/{tag}";
    }

    /// <summary>
    /// Builds the S3 key for an upload session.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="uploadId">The upload UUID.</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildUploadKey(string repositoryName, string uploadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        return $"repositories/{repositoryName}/uploads/{uploadId}";
    }

    /// <summary>
    /// Builds the S3 key for a referrers index.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="subjectDigest">The subject manifest digest.</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildReferrersIndexKey(string repositoryName, string subjectDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectDigest);
        return $"repositories/{repositoryName}/referrers/{subjectDigest}/index.json";
    }

    /// <summary>
    /// Builds the S3 key prefix for listing tags in a repository.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <returns>The S3 key prefix.</returns>
    public static string BuildTagsPrefix(string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        return $"repositories/{repositoryName}/tags/";
    }

    /// <summary>
    /// Builds the S3 key for upload session metadata.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="sessionKey">The session identifier.</param>
    /// <returns>The S3 key path.</returns>
    public static string BuildUploadSessionKey(string repositoryName, string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        return $"repositories/{repositoryName}/uploads/{sessionKey}/metadata.json";
    }

    /// <summary>
    /// Builds the S3 key prefix for upload data parts.
    /// </summary>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="sessionKey">The session identifier.</param>
    /// <returns>The S3 key prefix.</returns>
    public static string BuildUploadDataKey(string repositoryName, string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        return $"repositories/{repositoryName}/uploads/{sessionKey}/data";
    }
}
