using Dotreg.Core.Validation;

namespace Dotreg.Core.Models;

/// <summary>
/// Represents an OCI repository with validated name
/// </summary>
public class Repository
{
    private string _name = string.Empty;

    /// <summary>
    /// The name of the repository (e.g., library/nginx, myorg/myapp)
    /// </summary>
    public required string Name
    {
        get => _name;
        init
        {
            if (!NameValidator.IsValidRepositoryName(value))
            {
                throw new ArgumentException($"Invalid repository name: {value}", nameof(Name));
            }
            _name = value;
        }
    }

    /// <summary>
    /// Creates a new repository with the specified name
    /// </summary>
    public Repository() { }

    /// <summary>
    /// Creates a new repository with the specified name
    /// </summary>
    public Repository(string name)
    {
        Name = name;
    }
}
