using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("BRANDS")]
public class BrandsController : Controller
{
    private readonly IBrandService _brandService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public BrandsController(IBrandService brandService, IBusinessUnitService businessUnitService, ICurrentUserService currentUserService, ICompanyContextService companyContextService, IBusinessUnitContextService businessUnitContextService, IAuditLogService auditLogService)
    {
        _brandService = brandService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int? businessUnitId, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _brandService.GetPagedAsync(request, effectiveCompanyId, businessUnitId: businessUnitId);

        ViewData["Search"] = search;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<BusinessUnit>();

        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("BRANDS", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new Brand { CompanyId = effectiveCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("BRANDS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Brand model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        model.CompanyId = effectiveCompanyId;
        model.BusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        if (!ModelState.IsValid || model.CompanyId <= 0)
        {
            if (model.CompanyId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a brand.");
            }
            return View(model);
        }

        var result = await _brandService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create brand.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Brands", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Brand created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("BRANDS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var brand = await _brandService.GetByIdAsync(id);
        if (brand is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && brand.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(brand);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("BRANDS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Brand model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _brandService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update brand.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Brands", model.BrandId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Brand updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("BRANDS", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var brand = await _brandService.GetByIdAsync(id);
        if (brand is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && brand.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _brandService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Brands", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Brand deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

}
