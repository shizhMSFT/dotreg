namespace Dotreg.Api.Models;

/// <summary>
/// OCI Distribution Spec error response format.
/// </summary>
public class OciErrorResponse
{
    /// <summary>
    /// Gets or sets the list of errors.
    /// </summary>
    public List<ErrorDetail> Errors { get; set; } = new();
}

/// <summary>
/// Individual error detail in OCI format.
/// </summary>
public class ErrorDetail
{
    /// <summary>
    /// Gets or sets the error code (e.g., MANIFEST_UNKNOWN, BLOB_UNKNOWN).
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets additional error detail.
    /// </summary>
    public object? Detail { get; set; }
}

/// <summary>
/// OCI Distribution Spec error codes.
/// </summary>
public static class OciErrorCodes
{
    public const string BlobUnknown = "BLOB_UNKNOWN";
    public const string BlobUploadInvalid = "BLOB_UPLOAD_INVALID";
    public const string BlobUploadUnknown = "BLOB_UPLOAD_UNKNOWN";
    public const string DigestInvalid = "DIGEST_INVALID";
    public const string ManifestBlobUnknown = "MANIFEST_BLOB_UNKNOWN";
    public const string ManifestInvalid = "MANIFEST_INVALID";
    public const string ManifestUnknown = "MANIFEST_UNKNOWN";
    public const string NameInvalid = "NAME_INVALID";
    public const string NameUnknown = "NAME_UNKNOWN";
    public const string SizeInvalid = "SIZE_INVALID";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Denied = "DENIED";
    public const string Unsupported = "UNSUPPORTED";
    public const string TooManyRequests = "TOOMANYREQUESTS";
}
