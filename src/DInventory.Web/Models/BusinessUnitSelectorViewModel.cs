using DInventory.Domain.Entities;

namespace DInventory.Web.Models;

public class BusinessUnitSelectorViewModel
{
    public List<BusinessUnit> BusinessUnits { get; set; } = new();
    public int? SelectedBusinessUnitId { get; set; }

    /// <summary>True when there's nothing to choose right now: no company selected yet, the company
    /// has zero business units, or it has exactly one (auto-selected, nothing to pick).</summary>
    public bool Disabled { get; set; }

    /// <summary>Drives the placeholder text shown when Disabled and no options are listed.</summary>
    public bool HasCompany { get; set; }
}
