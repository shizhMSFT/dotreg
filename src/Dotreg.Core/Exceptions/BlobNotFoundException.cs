namespace Dotreg.Core.Exceptions;

/// <summary>
/// Exception thrown when a blob is not found.
/// </summary>
public class BlobNotFoundException : Exception
{
    public BlobNotFoundException(string repositoryName, string digest)
        : base($"Blob not found: {repositoryName}@{digest}")
    {
        RepositoryName = repositoryName;
        Digest = digest;
    }

    public string RepositoryName { get; }
    public string Digest { get; }
}
