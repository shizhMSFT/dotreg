using Dotreg.Core.Exceptions;
using Dotreg.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dotreg.Core.Tests.Services;

/// <summary>
/// Tests for IRegistryService implementation with mocked IStorageService
/// Tests business logic for manifest and blob retrieval
/// </summary>
public class RegistryServiceTests
{
    [Fact]
    public async Task GetManifestAsync_WithExistingManifest_ShouldReturnManifest()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var reference = "latest";

        // Act
        var manifest = await service.GetManifestAsync(name, reference);

        // Assert
        manifest.Should().NotBeNull();
        manifest.Digest.Should().StartWith("sha256:");
        manifest.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetManifestAsync_WithNonExistentManifest_ShouldThrowManifestNotFoundException()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var reference = "nonexistent";

        // Act
        var act = async () => await service.GetManifestAsync(name, reference);

        // Assert
        await act.Should().ThrowAsync<ManifestNotFoundException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task GetManifestAsync_WithInvalidName_ShouldThrowInvalidNameException()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "invalid..name";
        var reference = "latest";

        // Act
        var act = async () => await service.GetManifestAsync(name, reference);

        // Assert
        await act.Should().ThrowAsync<InvalidNameException>()
            .WithMessage("*invalid*");
    }

    [Fact]
    public async Task GetManifestAsync_WithTag_ShouldResolveToDigest()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var tag = "latest";

        // Act
        var manifest = await service.GetManifestAsync(name, tag);

        // Assert
        manifest.Digest.Should().StartWith("sha256:");
        manifest.Digest.Length.Should().Be(71, "sha256: prefix (7) + 64 hex chars");
    }

    [Fact]
    public async Task GetBlobAsync_WithExistingBlob_ShouldReturnBlob()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var blob = await service.GetBlobAsync(name, digest);

        // Assert
        blob.Should().NotBeNull();
        blob.Digest.Should().Be(digest);
        blob.Content.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBlobAsync_WithNonExistentBlob_ShouldThrowBlobNotFoundException()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var act = async () => await service.GetBlobAsync(name, digest);

        // Assert
        await act.Should().ThrowAsync<BlobNotFoundException>()
            .WithMessage("*0000000000000000*");
    }

    [Fact]
    public async Task GetBlobAsync_WithInvalidDigest_ShouldThrowArgumentException()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var digest = "invalid-digest";

        // Act
        var act = async () => await service.GetBlobAsync(name, digest);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*digest*");
    }

    [Fact]
    public async Task CheckManifestExistsAsync_WithExistingManifest_ShouldReturnTrue()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var reference = "latest";

        // Act
        var exists = await service.CheckManifestExistsAsync(name, reference);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckManifestExistsAsync_WithNonExistentManifest_ShouldReturnFalse()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var reference = "nonexistent";

        // Act
        var exists = await service.CheckManifestExistsAsync(name, reference);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBlobExistsAsync_WithExistingBlob_ShouldReturnTrue()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var exists = await service.CheckBlobExistsAsync(name, digest);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBlobExistsAsync_WithNonExistentBlob_ShouldReturnFalse()
    {
        // Arrange
        var mockStorage = CreateMockStorageService();
        var service = new RegistryService(mockStorage);
        var name = "library/nginx";
        var digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var exists = await service.CheckBlobExistsAsync(name, digest);

        // Assert
        exists.Should().BeFalse();
    }

    private IStorageService CreateMockStorageService()
    {
        var mock = new Mock<IStorageService>();
        
        // Setup successful manifest retrieval by digest
        var manifestContent = """{"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json"}"""u8.ToArray();
        mock.Setup(s => s.ExistsAsync(It.IsRegex("^manifests/.+/sha256:[a-f0-9]{64}$"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(s => s.GetAsync(It.IsRegex("^manifests/.+/sha256:[a-f0-9]{64}$"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifestContent);
        mock.Setup(s => s.GetContentTypeAsync(It.IsRegex("^manifests/.+/sha256:[a-f0-9]{64}$"), It.IsAny<CancellationToken>()))
            .ReturnsAsync("application/vnd.oci.image.manifest.v1+json");
        
        // Setup tag resolution
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        mock.Setup(s => s.ExistsAsync(It.IsRegex("^tags/.+/latest$"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(s => s.GetAsync(It.IsRegex("^tags/.+/latest$"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(digest));
        
        // Setup blob retrieval - but exclude all-zeros digest (nonexistent)
        var blobStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        mock.Setup(s => s.ExistsAsync(It.Is<string>(k => k.StartsWith("blobs/") && !k.Contains("0000000000000000000000000000000000000000000000000000000000000000")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(s => s.GetStreamAsync(It.Is<string>(k => k.StartsWith("blobs/") && !k.Contains("0000000000000000000000000000000000000000000000000000000000000000")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blobStream);
        mock.Setup(s => s.GetSizeAsync(It.Is<string>(k => k.StartsWith("blobs/") && !k.Contains("0000000000000000000000000000000000000000000000000000000000000000")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1024);
        
        // Setup non-existent resources (return false for existence checks with all-zeros digest)
        mock.Setup(s => s.ExistsAsync(It.Is<string>(k => k.Contains("0000000000000000000000000000000000000000000000000000000000000000")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        return mock.Object;
    }
}
