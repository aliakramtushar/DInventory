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
[PermissionAuthorize("EXPENSESUBCATEGORIES")]
public class ExpenseSubcategoriesController : Controller
{
    private readonly IExpenseSubcategoryService _expenseSubcategoryService;
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public ExpenseSubcategoriesController(
        IExpenseSubcategoryService expenseSubcategoryService,
        IExpenseCategoryService expenseCategoryService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _expenseSubcategoryService = expenseSubcategoryService;
        _expenseCategoryService = expenseCategoryService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int? expenseCategoryId, string? search, int? businessUnitId, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _expenseSubcategoryService.GetPagedAsync(request, effectiveCompanyId, expenseCategoryId, businessUnitId: businessUnitId);

        ViewData["Search"] = search;
        ViewData["ExpenseCategoryId"] = expenseCategoryId;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<BusinessUnit>();

        await PopulateCategoriesAsync(effectiveCompanyId);
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSESUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        await PopulateCategoriesAsync(effectiveCompanyId);
        return View(new ExpenseSubcategory { CompanyId = effectiveCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSESUBCATEGORIES", PermissionAction.Create)]
    public async Task<IActionResult> Create(ExpenseSubcategory model)
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
                ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating an expense subcategory.");
            }
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        var result = await _expenseSubcategoryService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create expense subcategory.");
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "ExpenseSubcategories", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense subcategory created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSESUBCATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var expenseSubcategory = await _expenseSubcategoryService.GetByIdAsync(id);
        if (expenseSubcategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expenseSubcategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        await PopulateCategoriesAsync();
        return View(expenseSubcategory);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSESUBCATEGORIES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(ExpenseSubcategory model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            return View(model);
        }

        var existing = await _expenseSubcategoryService.GetByIdAsync(model.ExpenseSubcategoryId);
        if (existing is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && existing.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _expenseSubcategoryService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update expense subcategory.");
            await PopulateCategoriesAsync();
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "ExpenseSubcategories", model.ExpenseSubcategoryId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense subcategory updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSESUBCATEGORIES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var expenseSubcategory = await _expenseSubcategoryService.GetByIdAsync(id);
        if (expenseSubcategory is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expenseSubcategory.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _expenseSubcategoryService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "ExpenseSubcategories", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Expense subcategory deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetByCategory(int expenseCategoryId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var subcategories = await _expenseSubcategoryService.GetAllAsync(currentUser.CompanyId, expenseCategoryId, onlyActive: true);
        return Json(subcategories.Select(s => new { s.ExpenseSubcategoryId, s.ExpenseSubcategoryName }));
    }

    private async Task PopulateCategoriesAsync(int? companyId = null)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = companyId ?? currentUser.CompanyId;
        ViewBag.ExpenseCategories = await _expenseCategoryService.GetAllAsync(effectiveCompanyId, onlyActive: true);
    }
}
