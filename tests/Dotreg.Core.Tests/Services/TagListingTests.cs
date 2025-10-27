using Dotreg.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dotreg.Core.Tests.Services;

/// <summary>
/// Tests for tag listing functionality in RegistryService
/// </summary>
public class TagListingTests
{
    [Fact]
    public async Task ListTagsAsync_WithMultipleTags_ReturnsInLexicalOrder()
    {
        // Arrange
        var mockStorage = new Mock<IStorageService>();
        var tagKeys = new List<string>
        {
            "tags/library/nginx/v2.0",
            "tags/library/nginx/latest",
            "tags/library/nginx/v1.0",
            "tags/library/nginx/v1.5"
        };
        mockStorage.Setup(s => s.ListKeysAsync("tags/library/nginx/", default))
            .ReturnsAsync(tagKeys);
        
        var service = new RegistryService(mockStorage.Object);
        var name = "library/nginx";

        // Act
        var result = await service.ListTagsAsync(name, 100, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(4);
        result.Should().BeInAscendingOrder();
        result[0].Should().Be("latest");
        result[1].Should().Be("v1.0");
        result[2].Should().Be("v1.5");
        result[3].Should().Be("v2.0");
    }

    [Fact]
    public async Task ListTagsAsync_WithMaxResults_ReturnsLimitedTags()
    {
        // Arrange
        var mockStorage = new Mock<IStorageService>();
        var tagKeys = new List<string>
        {
            "tags/library/nginx/v1.0",
            "tags/library/nginx/v2.0",
            "tags/library/nginx/v3.0"
        };
        mockStorage.Setup(s => s.ListKeysAsync("tags/library/nginx/", default))
            .ReturnsAsync(tagKeys);
        
        var service = new RegistryService(mockStorage.Object);
        var name = "library/nginx";

        // Act
        var result = await service.ListTagsAsync(name, 2, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListTagsAsync_WithStartAfter_ReturnsTagsAfterMarker()
    {
        // Arrange
        var mockStorage = new Mock<IStorageService>();
        var tagKeys = new List<string>
        {
            "tags/library/nginx/v1.0",
            "tags/library/nginx/v2.0",
            "tags/library/nginx/v3.0"
        };
        mockStorage.Setup(s => s.ListKeysAsync("tags/library/nginx/", default))
            .ReturnsAsync(tagKeys);
        
        var service = new RegistryService(mockStorage.Object);
        var name = "library/nginx";

        // Act
        var result = await service.ListTagsAsync(name, 100, "v1.0", CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("v2.0");
        result[1].Should().Be("v3.0");
    }

    [Fact]
    public async Task ListTagsAsync_WithEmptyRepository_ReturnsEmptyList()
    {
        // Arrange
        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.ListKeysAsync("tags/library/empty/", default))
            .ReturnsAsync(new List<string>());
        
        var service = new RegistryService(mockStorage.Object);
        var name = "library/empty";

        // Act
        var result = await service.ListTagsAsync(name, 100, null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
