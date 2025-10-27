using Dotreg.Core.Validation;

namespace Dotreg.Api.Middleware;

/// <summary>
/// Middleware that validates repository names from route parameters before request processing
/// Returns OCI-compliant error responses for invalid names
/// </summary>
public class RepositoryNameValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RepositoryNameValidationMiddleware> _logger;

    public RepositoryNameValidationMiddleware(
        RequestDelegate next,
        ILogger<RepositoryNameValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract repository name from route
        var name = context.GetRouteValue("name") as string;

        if (name != null && !NameValidator.IsValidRepositoryName(name))
        {
            _logger.LogWarning("Invalid repository name: {Name}", name);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                errors = new[]
                {
                    new
                    {
                        code = "NAME_INVALID",
                        message = $"Invalid repository name: {name}",
                        detail = name
                    }
                }
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
            return;
        }

        await _next(context);
    }
}
