using DInventory.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>
/// Backs the global Company + Business Unit selectors in the top navbar (see
/// CompanySelectorViewComponent / BusinessUnitSelectorViewComponent). SuperAdmin picks a company
/// (and, once one is picked, optionally a business unit within it) here, and every company-scoped
/// page in the app honors it via ICompanyContextService / IBusinessUnitContextService for the rest of
/// the login - no page carries its own companyId/businessUnitId query string any more. Every user sees
/// both selectors, but Switch is a no-op for anyone but SuperAdmin (pinned to their own company), and
/// SwitchBusinessUnit is a no-op for anyone with a fixed BusinessUnitId of their own (pinned to that
/// unit) - a company-scoped user with no fixed unit can still use SwitchBusinessUnit freely to search
/// or enter data under a specific business unit, or "whole company".
/// </summary>
[Authorize]
public class CompanyContextController : Controller
{
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;

    public CompanyContextController(ICompanyContextService companyContextService, IBusinessUnitContextService businessUnitContextService)
    {
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Switch(int companyId, string? returnUrl)
    {
        _companyContextService.SetSelectedCompanyId(companyId);
        return RedirectBack(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchBusinessUnit(int? businessUnitId, string? returnUrl)
    {
        await _businessUnitContextService.SetSelectedBusinessUnitIdAsync(businessUnitId is > 0 ? businessUnitId : null);
        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }
}
