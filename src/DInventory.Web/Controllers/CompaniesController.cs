using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>Manages tenants. Superuser-only (Users.CompanyId = 0) - a company-level Admin manages
/// their own Business Units (see BusinessUnitsController) but never other companies.</summary>
[Authorize(Roles = "SuperAdmin")]
public class CompaniesController : Controller
{
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public CompaniesController(ICompanyService companyService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _companyService = companyService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _companyService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    public IActionResult Create() => View(new Company());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Company model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _companyService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create company.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Companies", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Company created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var company = await _companyService.GetByIdAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Company model)
    {
        if (model.CompanyId == 0)
        {
            ModelState.AddModelError(string.Empty, "The built-in superuser company cannot be edited.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _companyService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update company.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Companies", model.CompanyId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Company updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _companyService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Companies", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Company deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
