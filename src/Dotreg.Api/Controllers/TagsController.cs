using Dotreg.Api.Models;
using Dotreg.Core.Exceptions;
using Dotreg.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dotreg.Api.Controllers;

/// <summary>
/// OCI tags listing endpoints
/// GET /v2/{name}/tags/list - list tags for a repository
/// </summary>
[ApiController]
[Route("v2/{name}/tags")]
public class TagsController : ControllerBase
{
    private readonly IRegistryService _registryService;
    private readonly ILogger<TagsController> _logger;

    public TagsController(IRegistryService registryService, ILogger<TagsController> logger)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists all tags for a repository with pagination support
    /// </summary>
    /// <param name="name">Repository name</param>
    /// <param name="n">Maximum number of results (default 100, max 1000)</param>
    /// <param name="last">Tag name to start after (for pagination)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("list")]
    public async Task<IActionResult> ListTags(
        string name,
        [FromQuery] int? n = null,
        [FromQuery] string? last = null,
        CancellationToken cancellationToken = default)
    {
        // Validate n parameter
        var maxResults = n ?? 100;
        if (maxResults < 1 || maxResults > 1000)
        {
            _logger.LogWarning(
                "Invalid n parameter for repository: {Repository}, Value: {N}",
                name,
                maxResults);
            return BadRequest(new OciErrorResponse
            {
                Errors = new List<ErrorDetail>
                {
                    new ErrorDetail
                    {
                        Code = OciErrorCodes.Unsupported,
                        Message = "Parameter 'n' must be between 1 and 1000",
                        Detail = $"Requested value: {maxResults}"
                    }
                }
            });
        }

        _logger.LogInformation(
            "Listing tags for repository: {Repository}, MaxResults: {MaxResults}, StartAfter: {StartAfter}",
            name,
            maxResults,
            last);

        try
        {
            var tags = await _registryService.ListTagsAsync(name, maxResults, last, cancellationToken);

            _logger.LogInformation(
                "Listed {Count} tags for repository: {Repository}",
                tags.Count,
                name);

            var tagList = new TagList
            {
                Name = name,
                Tags = tags
            };

            // Add Link header if there might be more results
            if (tags.Count == maxResults)
            {
                var lastTag = tags[^1];
                var nextUrl = $"{Request.Scheme}://{Request.Host}/v2/{name}/tags/list?n={maxResults}&last={Uri.EscapeDataString(lastTag)}";
                Response.Headers["Link"] = $"<{nextUrl}>; rel=\"next\"";
            }

            return Ok(tagList);
        }
        catch (InvalidNameException ex)
        {
            _logger.LogWarning(ex, "Invalid repository name: {Repository}", name);
            return BadRequest(new OciErrorResponse
            {
                Errors = new List<ErrorDetail>
                {
                    new ErrorDetail
                    {
                        Code = OciErrorCodes.NameInvalid,
                        Message = ex.Message
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tags for repository: {Repository}", name);
            throw;
        }
    }
}
