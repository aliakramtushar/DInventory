using DInventory.Application.Common.Interfaces;
using DInventory.Application.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>JWT-bearer secured JSON endpoint that feeds the Chart.js sales trend widget on the dashboard.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DashboardApiController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICompanyContextService _companyContextService;

    public DashboardApiController(IDashboardService dashboardService, ICompanyContextService companyContextService)
    {
        _dashboardService = dashboardService;
        _companyContextService = companyContextService;
    }

    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] string range = "daily", [FromQuery] int? businessUnitId = null)
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        // businessUnitId here is whatever the Dashboard page already resolved (auto-selected single
        // BU, the user's own BU, or a superuser's explicit pick) - trusted as-is since the companyId
        // filter above still constrains results to the right tenant even if it were mismatched.
        var points = await _dashboardService.GetTrendAsync(range, effectiveCompanyId, businessUnitId);
        return Ok(points);
    }
}
