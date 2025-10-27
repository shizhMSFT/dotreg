namespace Dotreg.Storage.S3;

/// <summary>
/// Configuration settings for AWS S3 storage.
/// </summary>
public class S3Config
{
    /// <summary>
    /// Gets or sets the S3 bucket name for registry storage.
    /// </summary>
    public string BucketName { get; set; } = "dotreg";

    /// <summary>
    /// Gets or sets the AWS region.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Gets or sets the S3 service URL (for LocalStack or custom S3-compatible services).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use path-style addressing for S3 (required for LocalStack).
    /// </summary>
    public bool UsePathStyle { get; set; }

    /// <summary>
    /// Gets or sets the AWS access key ID.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Gets or sets the AWS secret access key.
    /// </summary>
    public string? SecretAccessKey { get; set; }
}
