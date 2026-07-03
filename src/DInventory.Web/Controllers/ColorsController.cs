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
[PermissionAuthorize("COLORS")]
public class ColorsController : Controller
{
    private readonly IColorService _colorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public ColorsController(IColorService colorService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _colorService = colorService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _colorService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("COLORS", PermissionAction.Create)]
    public IActionResult Create() => View(new Color { DisplayOrder = 0 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("COLORS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Color model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _colorService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create color.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Colors", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Color created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("COLORS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var color = await _colorService.GetByIdAsync(id);
        if (color is null)
        {
            return NotFound();
        }

        return View(color);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("COLORS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Color model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _colorService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update color.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Colors", model.ColorId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Color updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("COLORS", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _colorService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Colors", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Color deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
