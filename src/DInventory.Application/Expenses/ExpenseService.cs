using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IExpenseCategoryRepository _expenseCategoryRepository;
    private readonly IExpenseSubcategoryRepository _expenseSubcategoryRepository;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IExpenseCategoryRepository expenseCategoryRepository,
        IExpenseSubcategoryRepository expenseSubcategoryRepository)
    {
        _expenseRepository = expenseRepository;
        _expenseCategoryRepository = expenseCategoryRepository;
        _expenseSubcategoryRepository = expenseSubcategoryRepository;
    }

    public Task<Expense?> GetByIdAsync(int expenseId) => _expenseRepository.GetByIdAsync(expenseId);

    public Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, int? expenseCategoryId = null, int companyId = 0)
        => _expenseRepository.GetPagedAsync(request, fromDate, toDate, expenseCategoryId, companyId);

    /// <summary>The expense category must belong to the same company the expense is being recorded
    /// under, and the subcategory (if any) must in turn belong to that category - mirrors
    /// ProductService's Category/Subcategory validation so a tampered or stale form field is always
    /// rejected server-side, not just filtered out of the dropdown on the client.</summary>
    private async Task<string?> ValidateCategoryAsync(int expenseCategoryId, int? expenseSubcategoryId, int companyId)
    {
        var category = await _expenseCategoryRepository.GetByIdAsync(expenseCategoryId);
        if (category is null || category.CompanyId != companyId)
        {
            return "The selected expense category is invalid.";
        }

        if (expenseSubcategoryId.HasValue)
        {
            var subcategory = await _expenseSubcategoryRepository.GetByIdAsync(expenseSubcategoryId.Value);
            if (subcategory is null || subcategory.ExpenseCategoryId != expenseCategoryId)
            {
                return "The selected expense subcategory does not belong to the selected category.";
            }
        }

        return null;
    }

    public async Task<Result<int>> CreateAsync(Expense expense, int? actingUserId)
    {
        if (expense.Amount <= 0)
        {
            return Result<int>.Failure("Amount must be greater than zero.");
        }

        var categoryError = await ValidateCategoryAsync(expense.ExpenseCategoryId, expense.ExpenseSubcategoryId, expense.CompanyId);
        if (categoryError is not null)
        {
            return Result<int>.Failure(categoryError);
        }

        expense.CreatedBy = actingUserId;
        expense.CreatedAt = DateTime.UtcNow;

        try
        {
            var id = await _expenseRepository.CreateAsync(expense);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save expense: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Expense expense, int? actingUserId)
    {
        var existing = await _expenseRepository.GetByIdAsync(expense.ExpenseId);
        if (existing is null)
        {
            return Result.Failure("Expense not found.");
        }

        if (expense.Amount <= 0)
        {
            return Result.Failure("Amount must be greater than zero.");
        }

        var categoryError = await ValidateCategoryAsync(expense.ExpenseCategoryId, expense.ExpenseSubcategoryId, existing.CompanyId);
        if (categoryError is not null)
        {
            return Result.Failure(categoryError);
        }

        existing.ExpenseDate = expense.ExpenseDate;
        existing.ExpenseCategoryId = expense.ExpenseCategoryId;
        existing.ExpenseSubcategoryId = expense.ExpenseSubcategoryId;
        existing.Amount = expense.Amount;
        existing.Remarks = expense.Remarks;
        // BusinessUnitId is intentionally left untouched - it's set once at creation from whichever
        // BU was selected in the topbar at the time, and there's no per-entry dropdown to change it
        // later (same rule as Category/Subcategory elsewhere in the app).

        try
        {
            var ok = await _expenseRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update expense.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update expense: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int expenseId)
    {
        try
        {
            var ok = await _expenseRepository.DeleteAsync(expenseId);
            return ok ? Result.Success() : Result.Failure("Unable to delete expense.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete expense: {ex.Message}");
        }
    }
}
