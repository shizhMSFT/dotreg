using System.Net;
using System.Text.Json;
using Dotreg.Api.Models;
using Dotreg.Core.Exceptions;

namespace Dotreg.Api.Middleware;

/// <summary>
/// Middleware that catches exceptions and converts them to OCI-formatted error responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            ManifestNotFoundException => (HttpStatusCode.NotFound, OciErrorCodes.ManifestUnknown, exception.Message),
            BlobNotFoundException => (HttpStatusCode.NotFound, OciErrorCodes.BlobUnknown, exception.Message),
            DigestMismatchException => (HttpStatusCode.BadRequest, OciErrorCodes.DigestInvalid, exception.Message),
            InvalidNameException => (HttpStatusCode.BadRequest, OciErrorCodes.NameInvalid, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, OciErrorCodes.ManifestInvalid, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "UNKNOWN", "An internal error occurred")
        };

        _logger.LogError(exception, "Error processing request: {ErrorCode} - {Message}", errorCode, message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var errorResponse = new OciErrorResponse
        {
            Errors = new List<ErrorDetail>
            {
                new ErrorDetail
                {
                    Code = errorCode,
                    Message = message
                }
            }
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
