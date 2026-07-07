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
[PermissionAuthorize("EXPENSES")]
public class ExpensesController : Controller
{
    private readonly IExpenseService _expenseService;
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly IExpenseSubcategoryService _expenseSubcategoryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public ExpensesController(
        IExpenseService expenseService,
        IExpenseCategoryService expenseCategoryService,
        IExpenseSubcategoryService expenseSubcategoryService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _expenseService = expenseService;
        _expenseCategoryService = expenseCategoryService;
        _expenseSubcategoryService = expenseSubcategoryService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? expenseCategoryId, int page = 1)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _expenseService.GetPagedAsync(request, from, to.AddDays(1), expenseCategoryId, effectiveCompanyId);

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");
        ViewData["ExpenseCategoryId"] = expenseCategoryId;
        ViewBag.ExpenseCategories = effectiveCompanyId > 0
            ? await _expenseCategoryService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<ExpenseCategory>();

        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        ViewBag.CanCreate = effectiveCompanyId > 0;

        await PopulateCategoriesAsync(effectiveCompanyId);
        return View(new Expense { ExpenseDate = DateTime.UtcNow.Date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Expense model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        model.CompanyId = effectiveCompanyId;
        // BusinessUnit is never picked per-entry - it always follows whatever is currently selected
        // in the topbar (same rule as every other module), so there's no dropdown on this form at all.
        model.BusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        if (model.CompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating an expense.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        var result = await _expenseService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create expense.");
            await PopulateCategoriesAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Expenses", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var expense = await _expenseService.GetByIdAsync(id);
        if (expense is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expense.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        await PopulateCategoriesAsync(expense.CompanyId);
        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Expense model)
    {
        var existingExpense = await _expenseService.GetByIdAsync(model.ExpenseId);
        if (existingExpense is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && existingExpense.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(existingExpense.CompanyId);
            return View(model);
        }

        var result = await _expenseService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update expense.");
            await PopulateCategoriesAsync(existingExpense.CompanyId);
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Expenses", model.ExpenseId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Expense updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSES", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _expenseService.GetByIdAsync(id);
        if (expense is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && expense.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _expenseService.DeleteAsync(id);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Expenses", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Expense deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Populates both the Category dropdown and the full Subcategory list (tagged with
    /// data-expense-category-id so client-side JS can filter it) for the given company - mirrors the
    /// Products Create/Edit cascading Category -> Subcategory pattern.</summary>
    private async Task PopulateCategoriesAsync(int companyId)
    {
        ViewBag.ExpenseCategories = companyId > 0
            ? await _expenseCategoryService.GetAllAsync(companyId, onlyActive: true)
            : Enumerable.Empty<ExpenseCategory>();
        ViewBag.ExpenseSubcategories = companyId > 0
            ? await _expenseSubcategoryService.GetAllAsync(companyId, onlyActive: true)
            : Enumerable.Empty<ExpenseSubcategory>();
    }
}
