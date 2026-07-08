using DInventory.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>
/// Serves a business unit's uploaded logo image. Split out from BusinessUnitsController (which is
/// [Authorize(Roles = "SuperAdmin,Admin")] for managing units) because the sidebar brand needs to
/// show a logo to every logged-in user regardless of role - stacking a role-less [Authorize] on a
/// single action of that controller would still AND together with the class-level role restriction,
/// not loosen it, so this lives in its own controller instead.
/// </summary>
[Authorize]
public class BusinessUnitLogoController : Controller
{
    private readonly IBusinessUnitService _businessUnitService;

    public BusinessUnitLogoController(IBusinessUnitService businessUnitService)
    {
        _businessUnitService = businessUnitService;
    }

    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Get(int id)
    {
        var logo = await _businessUnitService.GetLogoAsync(id);
        if (logo is null)
        {
            return NotFound();
        }

        return File(logo.Value.Data, logo.Value.ContentType);
    }
}
