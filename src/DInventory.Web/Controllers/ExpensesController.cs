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
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IAuditLogService _auditLogService;

    public ExpensesController(IExpenseService expenseService, IBusinessUnitService businessUnitService, ICurrentUserService currentUserService, ICompanyContextService companyContextService, IAuditLogService auditLogService)
    {
        _expenseService = expenseService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? category, int page = 1)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _expenseService.GetPagedAsync(request, from, to.AddDays(1), category, effectiveCompanyId);

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");
        ViewData["Category"] = category;
        ViewBag.Categories = IExpenseService.Categories;

        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        ViewBag.Categories = IExpenseService.Categories;
        ViewBag.CanCreate = effectiveCompanyId > 0;

        await PopulateBusinessUnitsAsync();
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

        if (model.CompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating an expense.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = IExpenseService.Categories;
            await PopulateBusinessUnitsAsync();
            return View(model);
        }

        var result = await _expenseService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create expense.");
            ViewBag.Categories = IExpenseService.Categories;
            await PopulateBusinessUnitsAsync();
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

        ViewBag.Categories = IExpenseService.Categories;
        await PopulateBusinessUnitsAsync();
        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSES", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Expense model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = IExpenseService.Categories;
            await PopulateBusinessUnitsAsync();
            return View(model);
        }

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

        var result = await _expenseService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update expense.");
            ViewBag.Categories = IExpenseService.Categories;
            await PopulateBusinessUnitsAsync();
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
        var result = await _expenseService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

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

    private async Task PopulateBusinessUnitsAsync()
    {
        var companyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.BusinessUnits = companyId > 0
            ? await _businessUnitService.GetAllAsync(companyId, onlyActive: true)
            : Enumerable.Empty<DInventory.Domain.Entities.BusinessUnit>();
    }
}
