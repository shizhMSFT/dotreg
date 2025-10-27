using Dotreg.Core.Validation;

namespace Dotreg.Api.Middleware;

/// <summary>
/// Middleware that validates digest parameters from routes
/// Returns OCI-compliant error responses for invalid digests
/// </summary>
public class DigestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DigestValidationMiddleware> _logger;

    public DigestValidationMiddleware(
        RequestDelegate next,
        ILogger<DigestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract digest from route (for blob endpoints)
        var digest = context.GetRouteValue("digest") as string;

        // Also check reference parameter (could be digest in manifest endpoints)
        var reference = context.GetRouteValue("reference") as string;
        
        // If reference looks like a digest (contains ':'), validate it
        if (reference?.Contains(':') == true)
        {
            digest = reference;
        }

        if (digest != null && digest.Contains(':') && !DigestValidator.IsValidDigest(digest))
        {
            _logger.LogWarning("Invalid digest: {Digest}", digest);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                errors = new[]
                {
                    new
                    {
                        code = "DIGEST_INVALID",
                        message = $"Invalid digest format: {digest}",
                        detail = digest
                    }
                }
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
            return;
        }

        await _next(context);
    }
}
