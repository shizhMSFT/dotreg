using Dotreg.Api.Controllers;
using Dotreg.Core.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
        
        // Create in-memory configuration
        var configDict = new Dictionary<string, string?>
        {
            { "Registry:MaxManifestSizeBytes", "4194304" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
        
        var controller = new ManifestsController(service, mockLogger.Object, configuration)
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
    [Fact]
    public async Task PutManifest_WithValidTag_ShouldReturn201Created()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var manifestContent = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json"}"""u8.ToArray();
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        
        mockService.Setup(s => s.PutManifestAsync(
            "library/nginx",
            "latest",
            It.IsAny<byte[]>(),
            "application/vnd.oci.image.manifest.v1+json",
            default))
            .ReturnsAsync(digest);
        
        var controller = CreateController(mockService.Object);
        controller.Request.ContentType = "application/vnd.oci.image.manifest.v1+json";
        controller.Request.Body = new MemoryStream(manifestContent);
        controller.Request.ContentLength = manifestContent.Length;

        // Act
        var result = await controller.PutManifest("library/nginx", "latest");

        // Assert
        result.Should().BeOfType<CreatedResult>();
        var createdResult = (CreatedResult)result;
        createdResult.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task PutManifest_WithDigestReference_ShouldReturn201Created()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var manifestContent = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json"}"""u8.ToArray();
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        
        mockService.Setup(s => s.PutManifestAsync(
            "library/nginx",
            digest,
            It.IsAny<byte[]>(),
            "application/vnd.oci.image.manifest.v1+json",
            default))
            .ReturnsAsync(digest);
        
        var controller = CreateController(mockService.Object);
        controller.Request.ContentType = "application/vnd.oci.image.manifest.v1+json";
        controller.Request.Body = new MemoryStream(manifestContent);
        controller.Request.ContentLength = manifestContent.Length;

        // Act
        var result = await controller.PutManifest("library/nginx", digest);

        // Assert
        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task DeleteManifest_WithDigest_Returns202Accepted()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(s => s.DeleteManifestAsync("library/nginx", "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", default))
            .Returns(Task.CompletedTask);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var result = await controller.DeleteManifest(name, digest);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task DeleteManifest_WithNonExistentManifest_Returns404NotFound()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(s => s.DeleteManifestAsync("library/nginx", "sha256:notfound", default))
            .ThrowsAsync(new Core.Exceptions.ManifestNotFoundException("library/nginx", "sha256:notfound"));
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";
        var digest = "sha256:notfound";

        // Act
        var result = await controller.DeleteManifest(name, digest);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteManifest_WhenDeletionDisabled_Returns405MethodNotAllowed()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(s => s.DeleteManifestAsync("library/nginx", "sha256:abcdef", default))
            .ThrowsAsync(new InvalidOperationException("Deletion is disabled"));
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";
        var digest = "sha256:abcdef";

        // Act
        var result = await controller.DeleteManifest(name, digest);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(405);
    }
}
