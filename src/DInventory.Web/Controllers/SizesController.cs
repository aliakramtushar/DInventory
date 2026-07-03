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
[PermissionAuthorize("SIZES")]
public class SizesController : Controller
{
    private readonly ISizeService _sizeService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public SizesController(ISizeService sizeService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _sizeService = sizeService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _sizeService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("SIZES", PermissionAction.Create)]
    public IActionResult Create() => View(new Size { DisplayOrder = 0 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SIZES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Size model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
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
        var result = await _sizeService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

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
