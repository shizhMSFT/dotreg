namespace Dotreg.Core.Exceptions;

/// <summary>
/// Exception thrown when a digest mismatch is detected.
/// </summary>
public class DigestMismatchException : Exception
{
    public DigestMismatchException(string expected, string actual)
        : base($"Digest mismatch: expected {expected}, got {actual}")
    {
        Expected = expected;
        Actual = actual;
    }

    public string Expected { get; }
    public string Actual { get; }
}
