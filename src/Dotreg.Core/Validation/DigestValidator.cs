using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dotreg.Core.Exceptions;

namespace Dotreg.Core.Validation;

/// <summary>
/// Validates and calculates content digests according to OCI spec.
/// </summary>
public static partial class DigestValidator
{
    // Digest format: algorithm:hex
    // Example: sha256:a3ed95caeb02ffe68cdd9fd84406680ae93d633cb16422d00e8a7c22955b46d4
    private static readonly Regex DigestRegex = CreateDigestRegex();

    [GeneratedRegex(@"^([a-z0-9]+(?:[+._-][a-z0-9]+)*):([a-fA-F0-9]{32,})$", RegexOptions.Compiled)]
    private static partial Regex CreateDigestRegex();

    /// <summary>
    /// Validates a digest format.
    /// </summary>
    /// <param name="digest">The digest string to validate.</param>
    /// <returns>True if valid.</returns>
    /// <exception cref="ArgumentException">Thrown if the digest format is invalid.</exception>
    public static bool ValidateDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            throw new ArgumentException("Digest cannot be empty", nameof(digest));
        }

        if (!DigestRegex.IsMatch(digest))
        {
            throw new ArgumentException($"Invalid digest format: {digest}", nameof(digest));
        }

        return true;
    }

    /// <summary>
    /// Calculates SHA256 digest of content.
    /// </summary>
    /// <param name="content">The content bytes.</param>
    /// <returns>The digest in format "sha256:hex".</returns>
    public static string CalculateSha256(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(content);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    /// <summary>
    /// Calculates SHA256 digest of a stream.
    /// </summary>
    /// <param name="stream">The content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The digest in format "sha256:hex".</returns>
    public static async Task<string> CalculateSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    /// <summary>
    /// Verifies that content matches the expected digest.
    /// </summary>
    /// <param name="content">The content bytes.</param>
    /// <param name="expectedDigest">The expected digest.</param>
    /// <exception cref="DigestMismatchException">Thrown if digests don't match.</exception>
    public static void VerifyDigest(byte[] content, string expectedDigest)
    {
        ValidateDigest(expectedDigest);
        var actualDigest = CalculateSha256(content);
        
        if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new DigestMismatchException(expectedDigest, actualDigest);
        }
    }

    /// <summary>
    /// Verifies that a stream matches the expected digest.
    /// </summary>
    /// <param name="stream">The content stream.</param>
    /// <param name="expectedDigest">The expected digest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DigestMismatchException">Thrown if digests don't match.</exception>
    public static async Task VerifyDigestAsync(Stream stream, string expectedDigest, CancellationToken cancellationToken = default)
    {
        ValidateDigest(expectedDigest);
        var actualDigest = await CalculateSha256Async(stream, cancellationToken);
        
        if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new DigestMismatchException(expectedDigest, actualDigest);
        }
    }

    /// <summary>
    /// Checks if a digest is valid without throwing.
    /// </summary>
    public static bool IsValidDigest(string digest)
    {
        try
        {
            ValidateDigest(digest);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
