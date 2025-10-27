namespace Dotreg.Core.Exceptions;

/// <summary>
/// Exception thrown when a manifest is not found.
/// </summary>
public class ManifestNotFoundException : Exception
{
    public ManifestNotFoundException(string repositoryName, string reference)
        : base($"Manifest not found: {repositoryName}@{reference}")
    {
        RepositoryName = repositoryName;
        Reference = reference;
    }

    public string RepositoryName { get; }
    public string Reference { get; }
}
