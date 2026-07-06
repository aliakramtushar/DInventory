using Microsoft.AspNetCore.Mvc.Filters;

namespace DInventory.Web.Filters;

/// <summary>
/// Forces every controller-rendered response (Razor views and JSON alike) to be Cache-Control:
/// no-store. Without this, a browser (or an intermediary proxy) is technically allowed to cache a
/// dynamic GET response and serve it again for a later request with different query-string
/// parameters in some caching configurations - which would make paginated list pages (or anything
/// else that depends on query-string state, like search/filter values) appear to "not update" even
/// though the server is genuinely computing and returning different data each time.
///
/// Static assets (css/js/images, served via UseStaticFiles) are NOT affected by this filter - MVC
/// filters only run for requests that reach the MVC action pipeline.
/// </summary>
public class NoCacheResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        headers["Pragma"] = "no-cache";
        headers["Expires"] = "-1";
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No-op - headers are only meaningful before the response starts writing.
    }
}
