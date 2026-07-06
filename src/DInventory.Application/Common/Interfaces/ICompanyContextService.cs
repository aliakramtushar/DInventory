using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

/// <summary>
/// Single source of truth for "which company's data should this request operate on" - replaces every
/// controller rolling its own `?companyId=` query-string logic. Backed by the global company selector
/// shown in the top navbar (see CompanySelectorViewComponent / CompanyContextController.SwitchCompany):
/// - Non-SuperAdmin users: always their own CompanyId. SetSelectedCompanyId is a no-op for them - they
///   can never change which company they operate on.
/// - SuperAdmin (CompanyId 0 - the built-in "Super Admin / All Companies" row): whichever real company
///   they last picked from the navbar dropdown, persisted in session for the rest of their login. 0
///   ("All Companies") is the default before they've picked anything - an aggregate/read-only view;
///   pages that create new records (Products, Sales, etc.) must still ask the SuperAdmin to pick one
///   real company if the session is still at 0, since a record can't belong to "All Companies".
/// </summary>
public interface ICompanyContextService
{
    /// <summary>True when the logged-in user is the built-in superuser (CompanyId 0) and therefore
    /// gets the global company picker instead of being pinned to one company.</summary>
    bool IsSuperCompany { get; }

    /// <summary>The company this request should scope its data to. 0 means "All Companies" (only
    /// possible for SuperAdmin, and only meaningful for read/list views).</summary>
    int GetEffectiveCompanyId();

    /// <summary>Persists the SuperAdmin's chosen company in session for the rest of this login.
    /// Silently ignored for non-SuperAdmin users.</summary>
    void SetSelectedCompanyId(int companyId);

    /// <summary>Every real (non-zero), active company a SuperAdmin can currently choose from.</summary>
    Task<IEnumerable<Company>> GetSelectableCompaniesAsync();
}
