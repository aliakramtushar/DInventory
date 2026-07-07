using DInventory.Application.Common.Interfaces;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

/// <summary>
/// Renders the global "which company am I working on" dropdown in the top navbar. SuperAdmin
/// (CompanyId 0, the built-in "Super Admin / All Companies" row) gets a live picker across every
/// company. Every other user is shown the same navbar slot but locked to their own company - visible
/// for context, not editable (see ICompanyContextService, which no-ops the setter for them too).
/// </summary>
public class CompanySelectorViewComponent : ViewComponent
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;

    public CompanySelectorViewComponent(ICurrentUserService currentUserService, ICompanyContextService companyContextService)
    {
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
        {
            return Content(string.Empty);
        }

        if (!currentUser.IsSuperCompany)
        {
            // Pinned to their own company everywhere - show it, locked.
            return View(new CompanySelectorViewModel
            {
                SelectedCompanyId = currentUser.CompanyId,
                SelectedCompanyName = currentUser.CompanyName,
                CanChange = false
            });
        }

        var companies = await _companyContextService.GetSelectableCompaniesAsync();
        var model = new CompanySelectorViewModel
        {
            Companies = companies.ToList(),
            SelectedCompanyId = _companyContextService.GetEffectiveCompanyId(),
            CanChange = true
        };

        return View(model);
    }
}
