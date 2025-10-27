using Dotreg.Api.Controllers;
using Dotreg.Core.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dotreg.Api.Tests;

/// <summary>
/// Tests for OCI blob endpoints
/// </summary>
public class BlobEndpointTests
{
    private static BlobsController CreateController(IRegistryService service)
    {
        var mockLogger = new Mock<ILogger<BlobsController>>();
        var controller = new BlobsController(service, mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetBlob_WithValidDigest_ShouldReturn200WithBlob()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var blobContent = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var blob = new Blob
        {
            Digest = digest,
            Size = 1024,
            Content = blobContent,
            MediaType = "application/octet-stream"
        };
        mockService.Setup(s => s.GetBlobAsync("library/nginx", digest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blob);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.GetBlob(name, digest, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileStreamResult>();
    }

    [Fact]
    public async Task HeadBlob_WithExistingBlob_ShouldReturn200WithHeaders()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var blobContent = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var blob = new Blob
        {
            Digest = digest,
            Size = 1024,
            Content = blobContent,
            MediaType = "application/octet-stream"
        };
        mockService.Setup(s => s.GetBlobAsync("library/nginx", digest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blob);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.HeadBlob(name, digest, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
    }
}
