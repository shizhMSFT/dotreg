namespace Dotreg.Core.Exceptions;

/// <summary>
/// Exception thrown when an upload session is not found.
/// </summary>
public class UploadSessionNotFoundException : Exception
{
    public Guid SessionId { get; }

    public UploadSessionNotFoundException(Guid sessionId)
        : base($"Upload session not found: {sessionId}")
    {
        SessionId = sessionId;
    }

    public UploadSessionNotFoundException(Guid sessionId, string message)
        : base(message)
    {
        SessionId = sessionId;
    }

    public UploadSessionNotFoundException(Guid sessionId, string message, Exception innerException)
        : base(message, innerException)
    {
        SessionId = sessionId;
    }
}
