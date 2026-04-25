using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using Contacts.Application.Common;

namespace Contacts.Api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Translates unhandled exceptions into RFC 7807 Problem Details responses.
/// Keeps stack traces out of API responses in production.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail) = exception switch
        {
            OperationCanceledException => (HttpStatusCode.ServiceUnavailable, "Request Cancelled", "The request was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Title = title,
            Status = (int)statusCode,
            Detail = (_env.IsDevelopment() || _env.IsEnvironment("Testing")) ? exception.Message : detail,
            Instance = context.Request.Path
        };

        problem.Extensions["correlationId"] =
            context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? "unknown";

        if (_env.IsDevelopment() || _env.IsEnvironment("Testing"))
            problem.Extensions["stackTrace"] = exception.StackTrace;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
