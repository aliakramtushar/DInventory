using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public BrandsController(IBrandService brandService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _brandService = brandService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _brandService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("BRANDS", PermissionAction.Create)]
    public IActionResult Create() => View(new Brand());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("BRANDS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Brand model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
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
        var result = await _brandService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

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
