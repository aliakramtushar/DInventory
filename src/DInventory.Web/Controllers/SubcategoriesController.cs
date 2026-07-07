using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Models;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("SUBCATEGORIES")]
public class SubcategoriesController : Controller
{
    private readonly ISubcategoryService _subcategoryService;
    private readonly ICategoryService _categoryService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public SubcategoriesController(
        ISubcategoryService subcategoryService,
        ICategoryService categoryService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _subcategoryService = subcategoryService;
        _categoryService = categoryService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int? categoryId, string? search, int? businessUnitId, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _subcategoryService.GetPagedAsync(request, effectiveCompanyId, categoryId, businessUnitId: businessUnitId);

        ViewData["Search"] = search;
        ViewData["CategoryId"] = categoryId;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<BusinessUnit>();

        await PopulateCategoriesAsync(effectiveCompanyId);
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        await PopulateCategoriesAsync(effectiveCompanyId);
        return View(new Subcategory { CompanyId = effectiveCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Subcategory model)
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
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a subcategory.");
            }
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        var result = await _subcategoryService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create subcategory.");
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Subcategories", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Subcategory created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var subcategory = await _subcategoryService.GetByIdAsync(id);
        if (subcategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && subcategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        await PopulateCategoriesAsync();
        return View(subcategory);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Subcategory model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _subcategoryService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update subcategory.");
            await PopulateCategoriesAsync();
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Subcategories", model.SubcategoryId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Subcategory updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var subcategory = await _subcategoryService.GetByIdAsync(id);
        if (subcategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && subcategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _subcategoryService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Subcategories", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Subcategory deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetByCategory(int categoryId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var subcategories = await _subcategoryService.GetAllAsync(currentUser.CompanyId, categoryId, onlyActive: true);
        return Json(subcategories.Select(s => new { s.SubcategoryId, s.SubcategoryName }));
    }

    private async Task PopulateCategoriesAsync(int? companyId = null)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = companyId ?? currentUser.CompanyId;
        ViewBag.Categories = await _categoryService.GetAllAsync(effectiveCompanyId, onlyActive: true);
    }

}
