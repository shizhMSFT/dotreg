using System.Text.RegularExpressions;
using Dotreg.Core.Exceptions;

namespace Dotreg.Core.Validation;

/// <summary>
/// Validates repository and tag names according to OCI Distribution Spec.
/// </summary>
public static partial class NameValidator
{
    // Repository name: lowercase alphanumeric, dots, dashes, underscores, slashes
    // Between 2-255 characters
    private static readonly Regex RepositoryNameRegex = CreateRepositoryNameRegex();

    // Tag name: alphanumeric, dots, dashes, underscores
    // Between 1-128 characters
    private static readonly Regex TagNameRegex = CreateTagNameRegex();

    [GeneratedRegex(@"^[a-z0-9]+([._\-][a-z0-9]+)*(/[a-z0-9]+([._\-][a-z0-9]+)*)*$", RegexOptions.Compiled)]
    private static partial Regex CreateRepositoryNameRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_][a-zA-Z0-9._\-]*$", RegexOptions.Compiled)]
    private static partial Regex CreateTagNameRegex();

    /// <summary>
    /// Validates a repository name.
    /// </summary>
    /// <param name="name">The repository name to validate.</param>
    /// <returns>True if valid.</returns>
    /// <exception cref="InvalidNameException">Thrown if the name is invalid.</exception>
    public static bool ValidateRepositoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidNameException(name ?? string.Empty, "Repository name cannot be empty");
        }

        if (name.Length < 2 || name.Length > 255)
        {
            throw new InvalidNameException(name, "Repository name must be between 2 and 255 characters");
        }

        if (!RepositoryNameRegex.IsMatch(name))
        {
            throw new InvalidNameException(name, "Repository name must be lowercase and contain only alphanumeric characters, dots, dashes, underscores, and slashes");
        }

        return true;
    }

    /// <summary>
    /// Validates a tag name.
    /// </summary>
    /// <param name="tag">The tag name to validate.</param>
    /// <returns>True if valid.</returns>
    /// <exception cref="InvalidNameException">Thrown if the tag is invalid.</exception>
    public static bool ValidateTagName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidNameException(tag ?? string.Empty, "Tag name cannot be empty");
        }

        if (tag.Length < 1 || tag.Length > 128)
        {
            throw new InvalidNameException(tag, "Tag name must be between 1 and 128 characters");
        }

        if (!TagNameRegex.IsMatch(tag))
        {
            throw new InvalidNameException(tag, "Tag name must contain only alphanumeric characters, dots, dashes, and underscores");
        }

        return true;
    }

    /// <summary>
    /// Checks if a repository name is valid without throwing.
    /// </summary>
    public static bool IsValidRepositoryName(string name)
    {
        try
        {
            ValidateRepositoryName(name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a tag name is valid without throwing.
    /// </summary>
    public static bool IsValidTagName(string tag)
    {
        try
        {
            ValidateTagName(tag);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
