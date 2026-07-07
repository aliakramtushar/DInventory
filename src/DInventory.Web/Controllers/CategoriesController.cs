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
[PermissionAuthorize("CATEGORIES")]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public CategoriesController(ICategoryService categoryService, IBusinessUnitService businessUnitService, ICurrentUserService currentUserService, ICompanyContextService companyContextService, IBusinessUnitContextService businessUnitContextService, IAuditLogService auditLogService)
    {
        _categoryService = categoryService;
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
        var result = await _categoryService.GetPagedAsync(request, effectiveCompanyId, businessUnitId: businessUnitId);

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
    [PermissionAuthorize("CATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new Category { CompanyId = effectiveCompanyId, BusinessUnitId = currentUser.BusinessUnitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Category model)
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
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a category.");
            }
            return View(model);
        }

        var result = await _categoryService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create category.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Categories", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("CATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && category.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Category model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _categoryService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update category.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Categories", model.CategoryId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CATEGORIES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && category.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _categoryService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Categories", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Category deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

}
