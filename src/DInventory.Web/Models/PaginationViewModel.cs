namespace DInventory.Web.Models;

/// <summary>
/// View model for the shared _Pagination partial. Carries just enough information (current page,
/// total pages, prev/next flags and any extra query-string values like search/filters) to render a
/// Bootstrap pagination control that preserves the current filters when navigating between pages.
/// </summary>
public class PaginationViewModel
{
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Extra route/query values (e.g. search, categoryId) to preserve across page links. Uses
    /// Dictionary&lt;string, string&gt; (non-nullable) because the asp-all-route-data tag helper
    /// binds to IDictionary&lt;string, string&gt; specifically. Build this with <see cref="Filters"/>
    /// so null/empty filter values are simply omitted instead of stored as null.
    /// </summary>
    public Dictionary<string, string> RouteValues { get; set; } = new();

    /// <summary>Defaults to the current action if not set.</summary>
    public string? ActionName { get; set; }

    /// <summary>Defaults to the current controller if not set.</summary>
    public string? ControllerName { get; set; }

    /// <summary>
    /// Builds a RouteValues dictionary from a set of possibly-null filter values (search box,
    /// dropdown filters, etc.), skipping any that are null/empty so they don't show up as literal
    /// "null" in the generated query string.
    /// </summary>
    public static Dictionary<string, string> Filters(params (string Key, string? Value)[] values)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                result[key] = value;
            }
        }

        return result;
    }
}
