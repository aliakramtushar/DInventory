using DInventory.Application.Common.Interfaces;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

/// <summary>
/// Renders the global "which company am I working on" dropdown in the top navbar - visible only to
/// SuperAdmin (CompanyId 0, the built-in "Super Admin / All Companies" row). Every other user never
/// sees this; they're silently pinned to their own company everywhere (see ICompanyContextService).
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
        if (!currentUser.IsAuthenticated || !currentUser.IsSuperCompany)
        {
            return Content(string.Empty);
        }

        var companies = await _companyContextService.GetSelectableCompaniesAsync();
        var model = new CompanySelectorViewModel
        {
            Companies = companies.ToList(),
            SelectedCompanyId = _companyContextService.GetEffectiveCompanyId()
        };

        return View(model);
    }
}
