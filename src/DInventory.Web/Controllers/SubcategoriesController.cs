using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Models;
using DInventory.Application.Common.Interfaces;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public SubcategoriesController(
        ISubcategoryService subcategoryService,
        ICategoryService categoryService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _subcategoryService = subcategoryService;
        _categoryService = categoryService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int? categoryId, string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _subcategoryService.GetPagedAsync(request, categoryId);

        ViewData["Search"] = search;
        ViewData["CategoryId"] = categoryId;
        await PopulateCategoriesAsync();
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateCategoriesAsync();
        return View(new Subcategory());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Subcategory model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _subcategoryService.CreateAsync(model, currentUser.UserId);

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
        var result = await _subcategoryService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

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
        var subcategories = await _subcategoryService.GetAllAsync(categoryId, onlyActive: true);
        return Json(subcategories.Select(s => new { s.SubcategoryId, s.SubcategoryName }));
    }

    private async Task PopulateCategoriesAsync()
    {
        ViewBag.Categories = await _categoryService.GetAllAsync(onlyActive: true);
    }
}
