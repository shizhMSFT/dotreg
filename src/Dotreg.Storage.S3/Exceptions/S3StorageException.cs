namespace Dotreg.Storage.S3.Exceptions;

/// <summary>
/// Exception thrown when an S3 storage operation fails.
/// </summary>
public class S3StorageException : Exception
{
    public S3StorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public S3StorageException(string operation, string key, Exception? innerException = null)
        : base($"S3 operation '{operation}' failed for key '{key}'", innerException)
    {
        Operation = operation;
        Key = key;
    }

    public string? Operation { get; }
    public string? Key { get; }
}
