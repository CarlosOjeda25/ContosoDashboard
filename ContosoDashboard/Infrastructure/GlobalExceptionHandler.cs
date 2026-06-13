using System.Text.Json;
using ContosoDashboard.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace ContosoDashboard.Infrastructure;

/// <summary>
/// Centralized exception handler — constitution §III.
/// Translates domain exceptions to their canonical HTTP status codes.
/// No stack traces are exposed in production responses.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            DocumentNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            ForbiddenException ex => (StatusCodes.Status403Forbidden, ex.Message),
            DocumentUploadException ex => MapUploadException(ex),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        // Log at the appropriate level — never expose PII in messages
        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "Domain exception handled: {StatusCode}", statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new
        {
            status = statusCode,
            title,
            instance = httpContext.Request.Path.Value
        };

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions),
            cancellationToken);

        return true;
    }

    private static (int statusCode, string title) MapUploadException(DocumentUploadException ex) =>
        ex.ErrorCode switch
        {
            DocumentUploadException.Codes.FileTooLarge => (StatusCodes.Status413RequestEntityTooLarge, "The uploaded file exceeds the maximum allowed size."),
            DocumentUploadException.Codes.InvalidMimeType => (StatusCodes.Status400BadRequest, "The file type is not permitted. Allowed types: PDF, DOCX, XLSX, PPTX, JPEG, PNG."),
            DocumentUploadException.Codes.InfectedFile => (StatusCodes.Status400BadRequest, "The uploaded file was rejected by the antivirus scanner."),
            DocumentUploadException.Codes.ScannerUnavailable => (StatusCodes.Status503ServiceUnavailable, "The antivirus scanning service is currently unavailable. Please try again later."),
            _ => (StatusCodes.Status400BadRequest, ex.Message)
        };
}
