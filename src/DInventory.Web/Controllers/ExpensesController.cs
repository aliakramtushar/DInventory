using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Expenses;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public ExpensesController(IExpenseService expenseService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _expenseService = expenseService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? category, int page = 1)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _expenseService.GetPagedAsync(request, from, to.AddDays(1), category);

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");
        ViewData["Category"] = category;
        ViewBag.Categories = IExpenseService.Categories;

        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("EXPENSES", PermissionAction.Create)]
    public IActionResult Create()
    {
        ViewBag.Categories = IExpenseService.Categories;
        return View(new Expense { ExpenseDate = DateTime.UtcNow.Date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("EXPENSES", PermissionAction.Create)]
    public async Task<IActionResult> Create(Expense model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = IExpenseService.Categories;
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _expenseService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create expense.");
            ViewBag.Categories = IExpenseService.Categories;
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

        ViewBag.Categories = IExpenseService.Categories;
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
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _expenseService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update expense.");
            ViewBag.Categories = IExpenseService.Categories;
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
}
