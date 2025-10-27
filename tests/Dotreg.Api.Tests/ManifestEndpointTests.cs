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
/// Tests for OCI manifest endpoints
/// </summary>
public class ManifestEndpointTests
{
    private static ManifestsController CreateController(IRegistryService service)
    {
        var mockLogger = new Mock<ILogger<ManifestsController>>();
        var controller = new ManifestsController(service, mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetManifest_WithValidTag_ShouldReturn200WithManifest()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var manifestContent = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = new Manifest
        {
            Digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            Content = manifestContent,
            MediaType = "application/vnd.oci.image.manifest.v1+json"
        };
        mockService.Setup(s => s.GetManifestAsync("library/nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";
        var reference = "latest";

        // Act
        var result = await controller.GetManifest(name, reference, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task GetManifest_WithValidDigest_ShouldReturn200WithManifest()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var manifestContent = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = new Manifest
        {
            Digest = digest,
            Content = manifestContent,
            MediaType = "application/vnd.oci.image.manifest.v1+json"
        };
        mockService.Setup(s => s.GetManifestAsync("library/nginx", digest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.GetManifest(name, digest, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task HeadManifest_WithExistingManifest_ShouldReturn200WithHeaders()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var manifestContent = new byte[] { 1, 2, 3, 4, 5 };
        var manifest = new Manifest
        {
            Digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            Content = manifestContent,
            MediaType = "application/vnd.oci.image.manifest.v1+json"
        };
        mockService.Setup(s => s.GetManifestAsync("library/nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";
        var reference = "latest";

        // Act
        var result = await controller.HeadManifest(name, reference, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
    }
}
