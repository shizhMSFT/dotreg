using Dotreg.Api.Controllers;
using Dotreg.Core.Exceptions;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dotreg.Api.Tests;

public class UploadEndpointTests
{
    private readonly Mock<IRegistryService> _mockRegistryService;
    private readonly Mock<IUploadSessionManager> _mockUploadSessionManager;
    private readonly Mock<ILogger<UploadsController>> _mockLogger;

    public UploadEndpointTests()
    {
        _mockRegistryService = new Mock<IRegistryService>();
        _mockUploadSessionManager = new Mock<IUploadSessionManager>();
        _mockLogger = new Mock<ILogger<UploadsController>>();
    }

    private UploadsController CreateController()
    {
        var controller = new UploadsController(
            _mockRegistryService.Object,
            _mockUploadSessionManager.Object,
            _mockLogger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    [Fact]
    public async Task InitiateUpload_ValidRepository_Returns202AcceptedWithLocationHeader()
    {
        // Arrange
        var repository = "myapp";
        var sessionId = Guid.NewGuid();
        
        _mockUploadSessionManager
            .Setup(m => m.CreateSessionAsync(repository, default))
            .ReturnsAsync(sessionId);

        var controller = CreateController();
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost:5000");

        // Act
        var result = await controller.InitiateUpload(repository);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        var acceptedResult = (AcceptedResult)result;
        acceptedResult.StatusCode.Should().Be(202);
        acceptedResult.Location.Should().Contain($"/v2/{repository}/blobs/uploads/{sessionId}");
    }

    [Fact]
    public async Task CompleteUpload_ValidDigest_Returns201CreatedWithLocationHeader()
    {
        // Arrange
        var repository = "myapp";
        var sessionId = Guid.NewGuid();
        var digest = "sha256:abcd1234567890abcd1234567890abcd1234567890abcd1234567890abcd1234";

        _mockUploadSessionManager
            .Setup(m => m.CompleteUploadAsync(sessionId, digest, null, default))
            .ReturnsAsync(digest);

        var controller = CreateController();
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost:5000");
        controller.Request.ContentLength = 0;

        // Act
        var result = await controller.CompleteUpload(repository, sessionId, digest);

        // Assert
        result.Should().BeOfType<CreatedResult>();
        var createdResult = (CreatedResult)result;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Location.Should().Contain($"/v2/{repository}/blobs/{digest}");
    }

    [Fact]
    public async Task CompleteUpload_DigestMismatch_ThrowsDigestMismatchException()
    {
        // Arrange
        var repository = "myapp";
        var sessionId = Guid.NewGuid();
        var digest = "sha256:wrongdigest1234567890abcd1234567890abcd1234567890abcd1234567890ab";

        _mockUploadSessionManager
            .Setup(m => m.CompleteUploadAsync(sessionId, digest, null, default))
            .ThrowsAsync(new DigestMismatchException(digest, "sha256:actual"));

        var controller = CreateController();
        controller.Request.ContentLength = 0;

        // Act
        Func<Task> act = async () => await controller.CompleteUpload(repository, sessionId, digest);

        // Assert
        await act.Should().ThrowAsync<DigestMismatchException>();
    }

    [Fact]
    public async Task GetUploadProgress_ExistingSession_Returns204WithRangeHeader()
    {
        // Arrange
        var repository = "myapp";
        var sessionId = Guid.NewGuid();
        var uploadedBytes = 1024L;

        _mockUploadSessionManager
            .Setup(m => m.GetUploadProgressAsync(sessionId, default))
            .ReturnsAsync(uploadedBytes);

        var controller = CreateController();

        // Act
        var result = await controller.GetUploadProgress(repository, sessionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CancelUpload_ExistingSession_Returns204NoContent()
    {
        // Arrange
        var repository = "myapp";
        var sessionId = Guid.NewGuid();

        _mockUploadSessionManager
            .Setup(m => m.CancelUploadAsync(sessionId, default))
            .Returns(Task.CompletedTask);

        var controller = CreateController();

        // Act
        var result = await controller.CancelUpload(repository, sessionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}
