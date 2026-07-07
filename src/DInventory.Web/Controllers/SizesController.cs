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
[PermissionAuthorize("SIZES")]
public class SizesController : Controller
{
    private readonly ISizeService _sizeService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public SizesController(ISizeService sizeService, IBusinessUnitService businessUnitService, ICurrentUserService currentUserService, ICompanyContextService companyContextService, IBusinessUnitContextService businessUnitContextService, IAuditLogService auditLogService)
    {
        _sizeService = sizeService;
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
        var result = await _sizeService.GetPagedAsync(request, effectiveCompanyId, businessUnitId);

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
    [PermissionAuthorize("SIZES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new Size { DisplayOrder = 0, CompanyId = effectiveCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SIZES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Size model)
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
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a size.");
            }
            return View(model);
        }

        var result = await _sizeService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create size.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Sizes", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Size created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("SIZES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var size = await _sizeService.GetByIdAsync(id);
        if (size is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && size.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(size);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SIZES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Size model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _sizeService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update size.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Sizes", model.SizeId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Size updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SIZES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var size = await _sizeService.GetByIdAsync(id);
        if (size is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && size.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _sizeService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Sizes", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Size deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

}
