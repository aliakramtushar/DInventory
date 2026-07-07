using System.Linq;
using DInventory.Application.Common.Interfaces;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

/// <summary>
/// Renders the global "which business unit within the selected company" dropdown in the top navbar,
/// right next to CompanySelectorViewComponent. Shown to every authenticated user, not just SuperAdmin:
/// - SuperAdmin: disabled until a real company is selected; auto-selected (and disabled) when that
///   company has exactly one business unit; otherwise picks among that company's units (or "Whole
///   Company").
/// - A company-scoped user with no fixed BusinessUnitId on their account: same live picker as
///   SuperAdmin, scoped to their own (fixed) company - they can search/enter data under a specific
///   business unit or leave it as "Whole Company".
/// - A user with a fixed BusinessUnitId on their account: pinned to that one unit everywhere, shown
///   disabled (nothing to pick).
/// </summary>
public class BusinessUnitSelectorViewComponent : ViewComponent
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;

    public BusinessUnitSelectorViewComponent(
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService)
    {
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
        {
            return Content(string.Empty);
        }

        var hasCompany = _companyContextService.GetEffectiveCompanyId() > 0;
        var units = await _businessUnitContextService.GetSelectableBusinessUnitsAsync();

        var isLockedToOwnUnit = !currentUser.IsSuperCompany && currentUser.BusinessUnitId.HasValue;
        if (isLockedToOwnUnit)
        {
            // Collapse the list to just their own unit (if it's still active/valid) so the view's
            // existing "exactly one unit" branch renders it disabled - they never get to pick.
            var ownUnit = units.FirstOrDefault(u => u.BusinessUnitId == currentUser.BusinessUnitId!.Value);
            units = ownUnit is not null
                ? new List<DInventory.Domain.Entities.BusinessUnit> { ownUnit }
                : Array.Empty<DInventory.Domain.Entities.BusinessUnit>();
        }

        var selected = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        var model = new BusinessUnitSelectorViewModel
        {
            BusinessUnits = units.ToList(),
            SelectedBusinessUnitId = selected,
            HasCompany = hasCompany,
            Disabled = isLockedToOwnUnit || !hasCompany || units.Count <= 1
        };

        return View(model);
    }
}
