using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Menus;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class MenusController : Controller
{
    private readonly IMenuService _menuService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public MenusController(IMenuService menuService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _menuService = menuService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _menuService.GetPagedAsync(request);

        // The list shows each menu's parent name (e.g. "Catalog") for readability - GetPagedAsync
        // only returns flat Menu rows, so resolve parent names from the full set here.
        var allMenus = (await _menuService.GetAllAsync()).ToDictionary(m => m.MenuId, m => m.MenuName);
        ViewBag.ParentNames = allMenus;

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateParentsAsync();
        return View(new Menu());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Menu model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentsAsync();
            return View(model);
        }

        var result = await _menuService.CreateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create menu.");
            await PopulateParentsAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Menus", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Menu created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var menu = await _menuService.GetByIdAsync(id);
        if (menu is null)
        {
            return NotFound();
        }

        await PopulateParentsAsync();
        return View(menu);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Menu model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentsAsync();
            return View(model);
        }

        var result = await _menuService.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update menu.");
            await PopulateParentsAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Menus", model.MenuId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Menu updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _menuService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Menus", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Menu deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateParentsAsync()
    {
        ViewBag.ParentMenus = await _menuService.GetAllAsync();
    }
}
