using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

/// <summary>
/// Companion to ICompanyContextService: resolves "which business unit within the effective company
/// should this request operate on" - shown in the same global navbar, right next to the Company
/// selector.
/// - No effective company selected yet (SuperAdmin still on "All Companies"): no business units to
///   choose from - always null, the navbar picker is disabled.
/// - Effective company has exactly one active business unit: that one, always - auto-selected, the
///   navbar picker is disabled (nothing to choose).
/// - Effective company has 2+ active business units, and the current user has no fixed BusinessUnitId
///   of their own (SuperAdmin, or a company-scoped user/admin): whichever one was picked this session
///   (cleared automatically whenever the effective company changes), or null ("whole company" - no
///   single business unit filter) until one is picked. They can search and enter data either way.
/// - A user with a fixed BusinessUnitId on their own account never sees a live picker: their effective
///   business unit is always that one (if it still belongs to their company), else null ("whole
///   company"), and SetSelectedBusinessUnitIdAsync is a no-op for them.
/// </summary>
public interface IBusinessUnitContextService
{
    /// <summary>Active business units belonging to the current effective company. Empty when no
    /// specific company is selected (or the company has none).</summary>
    Task<IReadOnlyList<BusinessUnit>> GetSelectableBusinessUnitsAsync();

    /// <summary>null = no single-business-unit filter (no company chosen, the company has none, or
    /// the user is viewing "whole company"). Otherwise the specific business unit to scope to.</summary>
    Task<int?> GetEffectiveBusinessUnitIdAsync();

    /// <summary>Persists the SuperAdmin's chosen business unit in session (silently ignored, both for
    /// non-SuperAdmin users and for any id that doesn't belong to the current effective company).</summary>
    Task SetSelectedBusinessUnitIdAsync(int? businessUnitId);
}
