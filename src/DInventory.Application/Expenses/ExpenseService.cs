using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;

    public ExpenseService(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public Task<Expense?> GetByIdAsync(int expenseId) => _expenseRepository.GetByIdAsync(expenseId);

    public Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, string? category = null, int companyId = 0)
        => _expenseRepository.GetPagedAsync(request, fromDate, toDate, category, companyId);

    public async Task<Result<int>> CreateAsync(Expense expense, int? actingUserId)
    {
        if (!IExpenseService.Categories.Contains(expense.Category))
        {
            return Result<int>.Failure("Unknown expense category.");
        }

        if (expense.Amount <= 0)
        {
            return Result<int>.Failure("Amount must be greater than zero.");
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

        if (!IExpenseService.Categories.Contains(expense.Category))
        {
            return Result.Failure("Unknown expense category.");
        }

        if (expense.Amount <= 0)
        {
            return Result.Failure("Amount must be greater than zero.");
        }

        existing.ExpenseDate = expense.ExpenseDate;
        existing.Category = expense.Category;
        existing.Amount = expense.Amount;
        existing.Remarks = expense.Remarks;
        existing.BusinessUnitId = expense.BusinessUnitId;

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
