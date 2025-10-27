using Dotreg.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Dotreg.Api.Tests;

/// <summary>
/// Tests for the OCI Distribution API version check endpoint GET /v2/
/// Per OCI spec, this endpoint MUST return 200 OK with Docker-Distribution-Api-Version header
/// </summary>
public class ApiVersionControllerTests
{
    private static ApiVersionController CreateController()
    {
        var controller = new ApiVersionController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetApiVersion_ShouldReturn200Ok()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetApiVersion();

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task GetApiVersion_ShouldReturnDockerDistributionApiVersionHeader()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetApiVersion();

        // Assert
        controller.Response.Headers["Docker-Distribution-Api-Version"].ToString()
            .Should().Be("registry/2.0");
    }

    [Fact]
    public async Task GetApiVersion_ShouldCompleteQuickly()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await controller.GetApiVersion();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "version endpoint must be fast as it's used for health checks");
    }
}
