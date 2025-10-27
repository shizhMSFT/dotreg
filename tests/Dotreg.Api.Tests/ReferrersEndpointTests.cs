using Dotreg.Api.Controllers;
using Dotreg.Api.Models;
using Dotreg.Core.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Dotreg.Api.Tests;

public class ReferrersEndpointTests
{
    private readonly Mock<IRegistryService> _mockRegistryService;
    private readonly Mock<ILogger<ReferrersController>> _mockLogger;
    private readonly ReferrersController _controller;

    public ReferrersEndpointTests()
    {
        _mockRegistryService = new Mock<IRegistryService>();
        _mockLogger = new Mock<ILogger<ReferrersController>>();
        _controller = new ReferrersController(_mockRegistryService.Object, _mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GetReferrers_WithExistingReferrers_Returns200WithImageIndex()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var referrers = new List<ReferrerDescriptor>
        {
            new ReferrerDescriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Size = 1234,
                ArtifactType = "application/vnd.example.signature.v1"
            },
            new ReferrerDescriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Size = 5678,
                ArtifactType = "application/vnd.example.sbom.v1"
            }
        };

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(name, digest, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referrers);

        // Act
        var result = await _controller.GetReferrers(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);

        var imageIndex = okResult.Value as ImageIndex;
        imageIndex.Should().NotBeNull();
        imageIndex!.SchemaVersion.Should().Be(2);
        imageIndex.MediaType.Should().Be("application/vnd.oci.image.index.v1+json");
        imageIndex.Manifests.Should().HaveCount(2);
        imageIndex.Manifests[0].ArtifactType.Should().Be("application/vnd.example.signature.v1");
        imageIndex.Manifests[1].ArtifactType.Should().Be("application/vnd.example.sbom.v1");
    }

    [Fact]
    public async Task GetReferrers_WithArtifactTypeFilter_Returns200WithFilteredResults()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var artifactType = "application/vnd.example.signature.v1";
        var referrers = new List<ReferrerDescriptor>
        {
            new ReferrerDescriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Size = 1234,
                ArtifactType = artifactType
            }
        };

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(name, digest, artifactType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referrers);

        // Act
        var result = await _controller.GetReferrers(name, digest, artifactType, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var imageIndex = okResult.Value as ImageIndex;
        imageIndex.Should().NotBeNull();
        imageIndex!.Manifests.Should().HaveCount(1);
        imageIndex.Manifests[0].ArtifactType.Should().Be(artifactType);

        // Verify OCI-Filters-Applied header
        _controller.Response.Headers.Should().ContainKey("OCI-Filters-Applied");
        _controller.Response.Headers["OCI-Filters-Applied"].ToString().Should().Be("artifactType");
    }

    [Fact]
    public async Task GetReferrers_WithNoReferrers_Returns200WithEmptyIndex()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(name, digest, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReferrerDescriptor>());

        // Act
        var result = await _controller.GetReferrers(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var imageIndex = okResult.Value as ImageIndex;
        imageIndex.Should().NotBeNull();
        imageIndex!.Manifests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReferrers_WithInvalidName_ReturnsBadRequest()
    {
        // Arrange
        var invalidName = "INVALID@NAME";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(invalidName, digest, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid repository name"));

        // Act
        var result = await _controller.GetReferrers(invalidName, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var errorResponse = badRequest.Value as OciErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.Should().ContainSingle();
        errorResponse.Errors[0].Code.Should().Be("NAME_INVALID");
    }

    [Fact]
    public async Task GetReferrers_WhenDisabled_Returns404()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(name, digest, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Referrers API is disabled"));

        // Act
        var result = await _controller.GetReferrers(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = (NotFoundObjectResult)result;
        var errorResponse = notFound.Value as OciErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.Errors.Should().ContainSingle();
        errorResponse.Errors[0].Code.Should().Be("UNSUPPORTED");
    }

    [Fact]
    public async Task GetReferrers_ReturnsCorrectContentType()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        _mockRegistryService
            .Setup(s => s.GetReferrersAsync(name, digest, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReferrerDescriptor>());

        // Act
        var result = await _controller.GetReferrers(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        // ContentType should be set to OCI Image Index media type
        _controller.Response.Headers.Should().ContainKey("Content-Type");
        _controller.Response.Headers["Content-Type"].ToString().Should().Contain("application/vnd.oci.image.index.v1+json");
    }
}
