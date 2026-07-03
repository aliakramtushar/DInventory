using System.Net;

namespace DInventory.Web.Middleware;

/// <summary>Global error handler: logs unhandled exceptions and shows a friendly error page (or JSON for API calls)
/// instead of letting the raw exception/stack trace reach the browser.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            var isApiRequest = context.Request.Path.StartsWithSegments("/api")
                || string.Equals(context.Request.Headers.Accept.ToString(), "application/json", StringComparison.OrdinalIgnoreCase);

            if (isApiRequest)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again." });
            }
            else
            {
                context.Response.Redirect("/Home/Error");
            }
        }
    }
}
