using Dotreg.Core.Exceptions;
using Dotreg.Core.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dotreg.Core.Tests.Services;

public class UploadSessionManagerTests
{
    private readonly Mock<IStorageService> _mockStorage;

    public UploadSessionManagerTests()
    {
        _mockStorage = new Mock<IStorageService>();
    }

    [Fact]
    public async Task CreateSessionAsync_ValidRepository_ReturnsNewSessionId()
    {
        // Arrange
        var repository = "myapp";
        _mockStorage
            .Setup(m => m.PutAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                default))
            .Returns(Task.CompletedTask);

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        var sessionId = await manager.CreateSessionAsync(repository);

        // Assert
        sessionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSessionAsync_StoresSessionMetadata()
    {
        // Arrange
        var repository = "myapp";
        
        _mockStorage
            .Setup(m => m.PutAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                default))
            .Returns(Task.CompletedTask);

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        var sessionId = await manager.CreateSessionAsync(repository);

        // Assert
        _mockStorage.Verify(
            m => m.PutAsync(
                It.Is<string>(s => s.Contains("_sessions")),
                It.IsAny<byte[]>(),
                "application/json",
                default),
            Times.Once);
    }

    [Fact]
    public async Task GetSessionAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = "myapp",
            ["created-at"] = DateTime.UtcNow.ToString("O"),
            ["expires-at"] = DateTime.UtcNow.AddHours(24).ToString("O"),
            ["uploaded-bytes"] = "0"
        };

        _mockStorage
            .Setup(m => m.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata));

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        var session = await manager.GetSessionAsync(sessionId);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().Be(sessionId);
        session.Repository.Should().Be("myapp");
    }

    [Fact]
    public async Task GetSessionAsync_NonExistentSession_ThrowsUploadSessionNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockStorage
            .Setup(m => m.GetAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new BlobNotFoundException("myapp", "session"));

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        Func<Task> act = async () => await manager.GetSessionAsync(sessionId);

        // Assert
        await act.Should().ThrowAsync<UploadSessionNotFoundException>();
    }

    [Fact]
    public async Task CompleteUploadAsync_ValidDigest_ReturnsDigest()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var digest = "sha256:2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae"; // SHA256 of "foo"
        var content = System.Text.Encoding.UTF8.GetBytes("foo");

        var sessionMetadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = "myapp",
            ["created-at"] = DateTime.UtcNow.ToString("O"),
            ["expires-at"] = DateTime.UtcNow.AddHours(24).ToString("O"),
            ["uploaded-bytes"] = "0"
        };

        _mockStorage
            .Setup(m => m.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(sessionMetadata));

        _mockStorage
            .Setup(m => m.GetUploadContentAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new MemoryStream(content));

        _mockStorage
            .Setup(m => m.StoreBlobAsync(It.IsAny<string>(), digest, It.IsAny<Stream>(), It.IsAny<Dictionary<string, string>>(), default))
            .Returns(Task.CompletedTask);

        _mockStorage
            .Setup(m => m.DeleteUploadSessionAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        var result = await manager.CompleteUploadAsync(sessionId, digest);

        // Assert
        result.Should().Be(digest);
    }

    [Fact]
    public async Task GetUploadProgressAsync_ExistingSession_ReturnsUploadedBytes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = "myapp",
            ["uploaded-bytes"] = "1024",
            ["created-at"] = DateTime.UtcNow.ToString("O"),
            ["expires-at"] = DateTime.UtcNow.AddHours(24).ToString("O")
        };

        _mockStorage
            .Setup(m => m.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata));

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        var progress = await manager.GetUploadProgressAsync(sessionId);

        // Assert
        progress.Should().Be(1024);
    }

    [Fact]
    public async Task CancelUploadAsync_ExistingSession_DeletesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["session-id"] = sessionId.ToString(),
            ["repository"] = "myapp",
            ["created-at"] = DateTime.UtcNow.ToString("O"),
            ["expires-at"] = DateTime.UtcNow.AddHours(24).ToString("O"),
            ["uploaded-bytes"] = "0"
        };

        _mockStorage
            .Setup(m => m.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata));

        _mockStorage
            .Setup(m => m.DeleteUploadSessionAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var manager = new UploadSessionManager(_mockStorage.Object);

        // Act
        await manager.CancelUploadAsync(sessionId);

        // Assert
        _mockStorage.Verify(
            m => m.DeleteUploadSessionAsync("myapp", sessionId.ToString(), default),
            Times.Once);
    }
}
