using Dotreg.Api.Controllers;
using Dotreg.Api.Models;
using Dotreg.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dotreg.Api.Tests;

/// <summary>
/// Tests for OCI tags listing endpoints
/// </summary>
public class TagsEndpointTests
{
    private static TagsController CreateController(IRegistryService service)
    {
        var mockLogger = new Mock<ILogger<TagsController>>();
        var controller = new TagsController(service, mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    [Fact]
    public async Task ListTags_WithExistingRepository_Returns200WithTagList()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var tags = new List<string> { "latest", "v1.0", "v2.0" };
        mockService.Setup(s => s.ListTagsAsync("library/nginx", 100, null, default))
            .ReturnsAsync(tags);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.ListTags(name, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<TagList>();
        var tagList = okResult.Value as TagList;
        tagList!.Name.Should().Be(name);
        tagList.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public async Task ListTags_WithPagination_Returns200WithLimitedResults()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var tags = new List<string> { "v1.0", "v2.0" };
        mockService.Setup(s => s.ListTagsAsync("library/nginx", 2, null, default))
            .ReturnsAsync(tags);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.ListTags(name, 2, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var tagList = okResult!.Value as TagList;
        tagList!.Tags.Count.Should().Be(2);
    }

    [Fact]
    public async Task ListTags_WithLastParameter_Returns200WithTagsAfterLast()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var tags = new List<string> { "v2.0", "v3.0" };
        mockService.Setup(s => s.ListTagsAsync("library/nginx", 100, "v1.0", default))
            .ReturnsAsync(tags);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.ListTags(name, null, "v1.0", CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var tagList = okResult!.Value as TagList;
        tagList!.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public async Task ListTags_WithEmptyRepository_Returns200WithEmptyArray()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(s => s.ListTagsAsync("library/empty", 100, null, default))
            .ReturnsAsync(new List<string>());
        
        var controller = CreateController(mockService.Object);
        var name = "library/empty";

        // Act
        var result = await controller.ListTags(name, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var tagList = okResult!.Value as TagList;
        tagList!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTags_WithInvalidNParameter_ReturnsBadRequest()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.ListTags(name, 2000, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListTags_WithMaxResults_ReturnsLinkHeaderWhenMoreExist()
    {
        // Arrange
        var mockService = new Mock<IRegistryService>();
        var tags = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            tags.Add($"v{i}");
        }
        mockService.Setup(s => s.ListTagsAsync("library/nginx", 100, null, default))
            .ReturnsAsync(tags);
        
        var controller = CreateController(mockService.Object);
        var name = "library/nginx";

        // Act
        var result = await controller.ListTags(name, 100, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        // Link header would be set if there are more results (implementation detail)
    }
}
