using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DInventory.Web.Filters;

/// <summary>
/// App-wide safety net for unhandled exceptions raised inside controller actions. Individual
/// services are expected to catch their own known failure modes and return a Result.Failure (see
/// ProductService.CreateAsync/UpdateAsync for the pattern) so the controller can redisplay the
/// same form with the real message via ModelState - this filter only exists to catch whatever
/// slips through that (a DB outage, a bug, an unexpected null, etc.) so the user never sees a
/// blank generic error page and silently loses their work.
///
/// AJAX/JSON requests get a JSON body with the real message. Everything else gets redirected back
/// to where they came from with TempData["ErrorMessage"] set, which Views/Shared/_Layout.cshtml
/// already renders as a dismissible red banner on the next page - the same mechanism actions like
/// Delete/ToggleActive already use.
///
/// ExceptionHandlingMiddleware (see DInventory.Web/Middleware) remains as an outer, last-resort net
/// for exceptions raised outside the MVC action pipeline (routing, auth, static files, etc.) where
/// this filter never runs.
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, ITempDataDictionaryFactory tempDataFactory)
    {
        _logger = logger;
        _tempDataFactory = tempDataFactory;
    }

    public void OnException(ExceptionContext context)
    {
        var request = context.HttpContext.Request;
        _logger.LogError(context.Exception, "Unhandled exception in {Method} {Path}", request.Method, request.Path);

        var message = context.Exception.Message;

        var wantsJson = request.Path.StartsWithSegments("/api")
            || string.Equals(request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false)
            || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (wantsJson)
        {
            context.Result = new ObjectResult(new { error = message })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            context.ExceptionHandled = true;
            return;
        }

        // Regular page request: send the user back to where they were with a visible error banner,
        // rather than a blank "Something went wrong" page that loses their place in the app.
        var tempData = _tempDataFactory.GetTempData(context.HttpContext);
        tempData["ErrorMessage"] = $"Something went wrong: {message}";

        var referer = request.Headers.Referer.ToString();
        var redirectUrl = !string.IsNullOrWhiteSpace(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            && string.Equals(refererUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
                ? referer
                : "/";

        context.Result = new RedirectResult(redirectUrl);
        context.ExceptionHandled = true;
    }
}
