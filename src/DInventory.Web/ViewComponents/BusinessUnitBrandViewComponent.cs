using DInventory.Application.Common.Interfaces;
using DInventory.Application.Tenancy;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

/// <summary>
/// Renders the sidebar's top brand block: the logged-in user's effective business unit's logo +
/// name when one is resolved (see IBusinessUnitContextService - same "which unit am I currently
/// operating under" resolution used by the navbar's Business Unit selector), falling back to the
/// generic "DInventory" brand otherwise (not logged in, no company selected yet, "Whole Company",
/// or the effective unit has no logo uploaded).
/// </summary>
public class BusinessUnitBrandViewComponent : ViewComponent
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IBusinessUnitService _businessUnitService;

    public BusinessUnitBrandViewComponent(
        ICurrentUserService currentUserService,
        IBusinessUnitContextService businessUnitContextService,
        IBusinessUnitService businessUnitService)
    {
        _currentUserService = currentUserService;
        _businessUnitContextService = businessUnitContextService;
        _businessUnitService = businessUnitService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
        {
            return View(new BusinessUnitBrandViewModel());
        }

        var effectiveBusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();
        if (!effectiveBusinessUnitId.HasValue)
        {
            return View(new BusinessUnitBrandViewModel());
        }

        var businessUnit = await _businessUnitService.GetByIdAsync(effectiveBusinessUnitId.Value);
        if (businessUnit is null)
        {
            return View(new BusinessUnitBrandViewModel());
        }

        return View(new BusinessUnitBrandViewModel
        {
            BusinessUnitId = businessUnit.BusinessUnitId,
            BusinessUnitName = businessUnit.BusinessUnitName,
            HasLogo = businessUnit.HasLogo
        });
    }
}
