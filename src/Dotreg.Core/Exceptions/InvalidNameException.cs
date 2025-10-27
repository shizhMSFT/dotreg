namespace Dotreg.Core.Exceptions;

/// <summary>
/// Exception thrown when an invalid name (repository or tag) is provided.
/// </summary>
public class InvalidNameException : Exception
{
    public InvalidNameException(string name, string reason)
        : base($"Invalid name '{name}': {reason}")
    {
        Name = name;
        Reason = reason;
    }

    public string Name { get; }
    public string Reason { get; }
}
