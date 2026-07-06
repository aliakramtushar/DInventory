using DInventory.Application.Common.Interfaces;
using DInventory.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;

    public DashboardController(
        IDashboardService dashboardService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
    }

    /// <summary>Company and Business Unit both now come entirely from the global navbar selectors
    /// (ICompanyContextService / IBusinessUnitContextService) - this page no longer carries its own
    /// pickers for either.</summary>
    public async Task<IActionResult> Index(string range = "today")
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var effectiveBusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        var stats = await _dashboardService.GetStatsAsync(effectiveCompanyId, effectiveBusinessUnitId, range);

        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = effectiveBusinessUnitId;
        ViewData["Range"] = range;

        return View(stats);
    }
}
