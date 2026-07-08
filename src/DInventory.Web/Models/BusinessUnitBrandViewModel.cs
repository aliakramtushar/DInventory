namespace DInventory.Web.Models;

/// <summary>View model for the sidebar's top brand block (see BusinessUnitBrandViewComponent).
/// BusinessUnitId is null (and HasLogo/BusinessUnitName unset) whenever there's no effective
/// business unit to show - the view falls back to the generic "DInventory" brand in that case.</summary>
public class BusinessUnitBrandViewModel
{
    public int? BusinessUnitId { get; set; }
    public string? BusinessUnitName { get; set; }
    public bool HasLogo { get; set; }
}
