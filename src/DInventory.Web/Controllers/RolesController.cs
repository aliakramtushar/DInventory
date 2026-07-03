using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Menus;
using DInventory.Application.Roles;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class RolesController : Controller
{
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public RolesController(
        IRoleService roleService,
        IPermissionService permissionService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _roleService = roleService;
        _permissionService = permissionService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _roleService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public IActionResult Create() => View(new Role());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Role model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _roleService.CreateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create role.");
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Roles", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Role created. Set its menu permissions from the Permissions page.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Role model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _roleService.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update role.");
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Roles", model.RoleId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Role updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _roleService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Roles", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Role deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(int id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        ViewBag.Role = role;
        var permissions = await _permissionService.GetPermissionsForRoleAsync(id);
        return View(permissions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(int id, List<RolePermissionUpdateItem> items)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        await _permissionService.SaveRolePermissionsAsync(id, items ?? new List<RolePermissionUpdateItem>());

        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE_PERMISSIONS", "RoleMenuPermissions", id.ToString(), ipAddress: currentUser.IpAddress);

        TempData["StatusMessage"] = $"Permissions updated for role '{role.RoleName}'.";
        return RedirectToAction(nameof(Permissions), new { id });
    }
}
