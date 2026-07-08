using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>Manages business units (branches/outlets) within a company. A normal company Admin only
/// ever sees/manages their own company's business units; a superuser (CompanyId 0) can manage any
/// company's via a company picker on the list page.</summary>
[Authorize(Roles = "SuperAdmin,Admin")]
public class BusinessUnitsController : Controller
{
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IAuditLogService _auditLogService;

    public BusinessUnitsController(
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IAuditLogService auditLogService)
    {
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _businessUnitService.GetPagedAsync(request, effectiveCompanyId);

        ViewData["Search"] = search;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new BusinessUnit { CompanyId = effectiveCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusinessUnit model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        model.CompanyId = effectiveCompanyId;

        if (!ModelState.IsValid || model.CompanyId <= 0)
        {
            if (model.CompanyId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a business unit.");
            }
            return View(model);
        }

        var result = await _businessUnitService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create business unit.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "BusinessUnits", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Business unit created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var businessUnit = await _businessUnitService.GetByIdAsync(id);
        if (businessUnit is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && businessUnit.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(businessUnit);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BusinessUnit model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingBusinessUnit = await _businessUnitService.GetByIdAsync(model.BusinessUnitId);
        if (existingBusinessUnit is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && existingBusinessUnit.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _businessUnitService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update business unit.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "BusinessUnits", model.BusinessUnitId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Business unit updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _businessUnitService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "BusinessUnits", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Business unit deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Uploads/replaces this business unit's logo. Same company-ownership check as Edit -
    /// class-level [Authorize(Roles = "SuperAdmin,Admin")] already restricts who can reach this at
    /// all; this additionally stops an Admin from one company touching another company's unit.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLogo(int id, IFormFile? logo)
    {
        var businessUnit = await _businessUnitService.GetByIdAsync(id);
        if (businessUnit is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && businessUnit.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        if (logo is null || logo.Length == 0)
        {
            TempData["ErrorMessage"] = "Please choose a JPG or PNG file to upload.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        byte[] data;
        using (var memoryStream = new MemoryStream())
        {
            await logo.CopyToAsync(memoryStream);
            data = memoryStream.ToArray();
        }

        var result = await _businessUnitService.UpdateLogoAsync(id, data, logo.ContentType, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "BusinessUnits", $"{id}:Logo", ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Logo updated.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo(int id)
    {
        var businessUnit = await _businessUnitService.GetByIdAsync(id);
        if (businessUnit is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && businessUnit.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _businessUnitService.UpdateLogoAsync(id, null, null, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "BusinessUnits", $"{id}:LogoRemoved", ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Logo removed.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }
}
