namespace Contacts.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation ID.
/// Reads from the X-Correlation-Id request header (set by API gateway or client),
/// or generates a new one if absent. Echoes it back in the response header
/// so callers can trace distributed logs across services.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Add to response header before processing so it's present even on error paths
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push into the logging scope so all Serilog logs in this request carry it
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
