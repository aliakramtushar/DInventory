using DInventory.Application.Common.Interfaces;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

/// <summary>
/// Renders the global "which business unit within the selected company" dropdown in the top navbar -
/// visible only to SuperAdmin, right next to CompanySelectorViewComponent. Disabled until a real
/// company is selected; auto-selected (and disabled) when that company has exactly one business unit;
/// otherwise lets SuperAdmin pick among that company's business units (or "Whole Company").
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
        if (!currentUser.IsAuthenticated || !currentUser.IsSuperCompany)
        {
            return Content(string.Empty);
        }

        var hasCompany = _companyContextService.GetEffectiveCompanyId() > 0;
        var units = await _businessUnitContextService.GetSelectableBusinessUnitsAsync();
        var selected = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        var model = new BusinessUnitSelectorViewModel
        {
            BusinessUnits = units.ToList(),
            SelectedBusinessUnitId = selected,
            HasCompany = hasCompany,
            Disabled = !hasCompany || units.Count <= 1
        };

        return View(model);
    }
}
