using DInventory.Application.Audit;
using DInventory.Application.Auth;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Roles;
using DInventory.Application.Users;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public UsersController(
        IUserService userService,
        IRoleService roleService,
        IAuthService authService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _userService = userService;
        _roleService = roleService;
        _authService = authService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _userService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateRolesAsync();
        return View(new User());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(User model, string password)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _userService.CreateAsync(model, password, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create user.");
            await PopulateRolesAsync();
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Users", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        await PopulateRolesAsync();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(User model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync();
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _userService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update user.");
            await PopulateRolesAsync();
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Users", model.UserId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _userService.DeleteAsync(id, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Users", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "User deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, bool isActive)
    {
        await _userService.ToggleActiveAsync(id, isActive);
        var currentUser = _currentUserService.GetCurrentUser();
        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, isActive ? "ACTIVATE" : "DEACTIVATE", "Users", id.ToString(), ipAddress: currentUser.IpAddress);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        var result = await _userService.AdminResetPasswordAsync(id, newPassword);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "RESET_PASSWORD", "Users", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Password has been reset.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Force-logout: revokes every active refresh token for this user ("cancel token").</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(int id)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var count = await _authService.RevokeAllTokensForUserAsync(id, currentUser.IpAddress);

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "REVOKE_TOKENS", "Users", id.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = $"Revoked {count} active session(s) for this user.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateRolesAsync()
    {
        ViewBag.Roles = await _roleService.GetAllAsync();
    }
}
