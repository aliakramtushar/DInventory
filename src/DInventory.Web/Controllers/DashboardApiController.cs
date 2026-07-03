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

    public DashboardApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] string range = "daily")
    {
        var points = await _dashboardService.GetTrendAsync(range);
        return Ok(points);
    }
}
