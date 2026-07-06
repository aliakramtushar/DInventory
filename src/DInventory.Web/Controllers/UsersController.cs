using DInventory.Application.Audit;
using DInventory.Application.Auth;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Roles;
using DInventory.Application.Tenancy;
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
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public UsersController(
        IUserService userService,
        IRoleService roleService,
        IAuthService authService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _userService = userService;
        _roleService = roleService;
        _authService = authService;
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

        // effectiveCompanyId 0 (SuperAdmin still on "All Companies") means "no company filter" for
        // this paged query, same as the old (companyId: null) behavior.
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _userService.GetPagedAsync(request, effectiveCompanyId > 0 ? effectiveCompanyId : null, businessUnitId);

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
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        await PopulateRolesAsync();
        var effectiveBusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();
        return View(new User { CompanyId = effectiveCompanyId, BusinessUnitId = effectiveBusinessUnitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(User model, string password)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;

        // Which company (and business unit) this new user belongs to always comes from the global
        // Company / Business Unit selectors in the top navbar now - never from a per-page picker. A
        // company-level admin was already pinned to their own company; a SuperAdmin must pick a real
        // company in the navbar first.
        model.CompanyId = effectiveCompanyId;
        model.BusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        if (model.CompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a user.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync();
            return View(model);
        }

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

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && user.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        await PopulateRolesAsync();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(User model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany)
        {
            // Same guard as Create - a company admin can't move a user to another company or to
            // the superuser company, even by tampering with the (hidden, for them) form field.
            model.CompanyId = currentUser.CompanyId;
        }

        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync();
            return View(model);
        }

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
