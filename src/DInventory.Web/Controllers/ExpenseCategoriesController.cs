using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Expenses;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("EXPENSECATEGORIES")]
public class ExpenseCategoriesController : Controller
{
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public ExpenseCategoriesController(
        IExpenseCategoryService expenseCategoryService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _expenseCategoryService = expenseCategoryService;
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

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _expenseCategoryService.GetPagedAsync(request, effectiveCompanyId, businessUnitId: businessUnitId);

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
    [PermissionAuthorize("EXPENSECATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new ExpenseCategory { CompanyId = effectiveCompanyId, BusinessUnitId = currentUser.BusinessUnitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSECATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create(ExpenseCategory model)
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
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating an expense category.");
            }
            return View(model);
        }

        var result = await _expenseCategoryService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create expense category.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "ExpenseCategories", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense category created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSECATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var expenseCategory = await _expenseCategoryService.GetByIdAsync(id);
        if (expenseCategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expenseCategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(expenseCategory);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSECATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(ExpenseCategory model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _expenseCategoryService.GetByIdAsync(model.ExpenseCategoryId);
        if (existing is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && existing.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _expenseCategoryService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update expense category.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "ExpenseCategories", model.ExpenseCategoryId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSECATEGORIES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var expenseCategory = await _expenseCategoryService.GetByIdAsync(id);
        if (expenseCategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expenseCategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _expenseCategoryService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "ExpenseCategories", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Expense category deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
