using Dotreg.Core.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text.Json;

namespace Dotreg.Core.Tests.Services;

public class ReferrersTests
{
    private readonly Mock<IStorageService> _mockStorage;
    private readonly IConfiguration _configuration;
    private readonly RegistryService _service;

    public ReferrersTests()
    {
        _mockStorage = new Mock<IStorageService>();
        
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Registry:EnableReferrersApi", "true"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
            
        _service = new RegistryService(_mockStorage.Object, _configuration);
    }

    [Fact]
    public async Task GetReferrersAsync_WithExistingReferrers_ReturnsListOfDescriptors()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        
        var referrersIndex = new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.index.v1+json",
            manifests = new[]
            {
                new
                {
                    mediaType = "application/vnd.oci.image.manifest.v1+json",
                    digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    size = 1234,
                    artifactType = "application/vnd.example.signature.v1"
                },
                new
                {
                    mediaType = "application/vnd.oci.image.manifest.v1+json",
                    digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    size = 5678,
                    artifactType = "application/vnd.example.sbom.v1"
                }
            }
        };

        var indexJson = JsonSerializer.Serialize(referrersIndex);
        var indexBytes = System.Text.Encoding.UTF8.GetBytes(indexJson);

        _mockStorage
            .Setup(s => s.ExistsAsync($"referrers/{name}/{digest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockStorage
            .Setup(s => s.GetStreamAsync($"referrers/{name}/{digest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(indexBytes));

        // Act
        var result = await _service.GetReferrersAsync(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Digest.Should().Be("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        result[0].ArtifactType.Should().Be("application/vnd.example.signature.v1");
        result[1].Digest.Should().Be("sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        result[1].ArtifactType.Should().Be("application/vnd.example.sbom.v1");
    }

    [Fact]
    public async Task GetReferrersAsync_WithArtifactTypeFilter_ReturnsFilteredResults()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var filterType = "application/vnd.example.signature.v1";
        
        var referrersIndex = new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.index.v1+json",
            manifests = new[]
            {
                new
                {
                    mediaType = "application/vnd.oci.image.manifest.v1+json",
                    digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    size = 1234,
                    artifactType = "application/vnd.example.signature.v1"
                },
                new
                {
                    mediaType = "application/vnd.oci.image.manifest.v1+json",
                    digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    size = 5678,
                    artifactType = "application/vnd.example.sbom.v1"
                }
            }
        };

        var indexJson = JsonSerializer.Serialize(referrersIndex);
        var indexBytes = System.Text.Encoding.UTF8.GetBytes(indexJson);

        _mockStorage
            .Setup(s => s.ExistsAsync($"referrers/{name}/{digest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockStorage
            .Setup(s => s.GetStreamAsync($"referrers/{name}/{digest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(indexBytes));

        // Act
        var result = await _service.GetReferrersAsync(name, digest, filterType, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ArtifactType.Should().Be(filterType);
    }

    [Fact]
    public async Task GetReferrersAsync_WhenIndexDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        _mockStorage
            .Setup(s => s.ExistsAsync($"referrers/{name}/{digest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.GetReferrersAsync(name, digest, null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReferrersAsync_WhenDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var disabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Registry:EnableReferrersApi", "false"}
            }!)
            .Build();
            
        var service = new RegistryService(_mockStorage.Object, disabledConfig);
        var name = "myorg/myapp";
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        // Act & Assert
        await service.Invoking(s => s.GetReferrersAsync(name, digest, null, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Referrers API is disabled*");
    }

    [Fact]
    public async Task UpdateReferrersIndexAsync_WithNewReferrer_AddsToIndex()
    {
        // Arrange
        var name = "myorg/myapp";
        var subjectDigest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var referrerDescriptor = new ReferrerDescriptor
        {
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            Size = 1234,
            ArtifactType = "application/vnd.example.signature.v1"
        };

        // Existing index is empty
        _mockStorage
            .Setup(s => s.ExistsAsync($"referrers/{name}/{subjectDigest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        string? capturedJson = null;
        _mockStorage
            .Setup(s => s.PutAsync(
                $"referrers/{name}/{subjectDigest}/index.json",
                It.IsAny<byte[]>(),
                "application/vnd.oci.image.index.v1+json",
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], string, CancellationToken>((_, bytes, __, ___) =>
            {
                capturedJson = System.Text.Encoding.UTF8.GetString(bytes);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateReferrersIndexAsync(name, subjectDigest, referrerDescriptor, CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.PutAsync(
            $"referrers/{name}/{subjectDigest}/index.json",
            It.IsAny<byte[]>(),
            "application/vnd.oci.image.index.v1+json",
            It.IsAny<CancellationToken>()), Times.Once);

        capturedJson.Should().NotBeNull();
        var indexDoc = JsonDocument.Parse(capturedJson!);
        indexDoc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        indexDoc.RootElement.GetProperty("mediaType").GetString().Should().Be("application/vnd.oci.image.index.v1+json");
        indexDoc.RootElement.GetProperty("manifests").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task UpdateReferrersIndexAsync_WithExistingIndex_AppendsToIndex()
    {
        // Arrange
        var name = "myorg/myapp";
        var subjectDigest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var newReferrer = new ReferrerDescriptor
        {
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            Size = 5678,
            ArtifactType = "application/vnd.example.sbom.v1"
        };

        // Existing index with one referrer
        var existingIndex = new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.index.v1+json",
            manifests = new[]
            {
                new
                {
                    mediaType = "application/vnd.oci.image.manifest.v1+json",
                    digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    size = 1234,
                    artifactType = "application/vnd.example.signature.v1"
                }
            }
        };

        var existingJson = JsonSerializer.Serialize(existingIndex);
        var existingBytes = System.Text.Encoding.UTF8.GetBytes(existingJson);

        _mockStorage
            .Setup(s => s.ExistsAsync($"referrers/{name}/{subjectDigest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockStorage
            .Setup(s => s.GetStreamAsync($"referrers/{name}/{subjectDigest}/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(existingBytes));

        string? capturedJson = null;
        _mockStorage
            .Setup(s => s.PutAsync(
                $"referrers/{name}/{subjectDigest}/index.json",
                It.IsAny<byte[]>(),
                "application/vnd.oci.image.index.v1+json",
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], string, CancellationToken>((_, bytes, __, ___) =>
            {
                capturedJson = System.Text.Encoding.UTF8.GetString(bytes);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateReferrersIndexAsync(name, subjectDigest, newReferrer, CancellationToken.None);

        // Assert
        capturedJson.Should().NotBeNull();
        var indexDoc = JsonDocument.Parse(capturedJson!);
        indexDoc.RootElement.GetProperty("manifests").GetArrayLength().Should().Be(2);
    }
}
