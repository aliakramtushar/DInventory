using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IExpenseCategoryRepository _expenseCategoryRepository;

    public ExpenseCategoryService(IExpenseCategoryRepository expenseCategoryRepository)
    {
        _expenseCategoryRepository = expenseCategoryRepository;
    }

    public Task<ExpenseCategory?> GetByIdAsync(int expenseCategoryId) => _expenseCategoryRepository.GetByIdAsync(expenseCategoryId);

    public Task<IEnumerable<ExpenseCategory>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null)
        => _expenseCategoryRepository.GetAllAsync(companyId, search, onlyActive, businessUnitId);

    public Task<PagedResult<ExpenseCategory>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null)
        => _expenseCategoryRepository.GetPagedAsync(request, companyId, onlyActive, businessUnitId);

    public async Task<Result<int>> CreateAsync(ExpenseCategory expenseCategory, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(expenseCategory.ExpenseCategoryName))
        {
            return Result<int>.Failure("Category name is required.");
        }

        if (await _expenseCategoryRepository.NameExistsAsync(expenseCategory.CompanyId, expenseCategory.ExpenseCategoryName))
        {
            return Result<int>.Failure("An expense category with this name already exists.");
        }

        expenseCategory.CreatedBy = actingUserId;
        expenseCategory.CreatedAt = DateTime.UtcNow;
        expenseCategory.IsActive = true;

        try
        {
            var id = await _expenseCategoryRepository.CreateAsync(expenseCategory);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save expense category: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(ExpenseCategory expenseCategory, int? actingUserId)
    {
        var existing = await _expenseCategoryRepository.GetByIdAsync(expenseCategory.ExpenseCategoryId);
        if (existing is null)
        {
            return Result.Failure("Expense category not found.");
        }

        if (string.IsNullOrWhiteSpace(expenseCategory.ExpenseCategoryName))
        {
            return Result.Failure("Category name is required.");
        }

        if (await _expenseCategoryRepository.NameExistsAsync(existing.CompanyId, expenseCategory.ExpenseCategoryName, expenseCategory.ExpenseCategoryId))
        {
            return Result.Failure("An expense category with this name already exists.");
        }

        existing.ExpenseCategoryName = expenseCategory.ExpenseCategoryName;
        existing.Description = expenseCategory.Description;
        existing.IsActive = expenseCategory.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _expenseCategoryRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update expense category.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update expense category: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int expenseCategoryId)
    {
        if (await _expenseCategoryRepository.HasExpensesAsync(expenseCategoryId))
        {
            return Result.Failure("Cannot delete an expense category that still has expenses recorded against it.");
        }

        try
        {
            var ok = await _expenseCategoryRepository.DeleteAsync(expenseCategoryId);
            return ok ? Result.Success() : Result.Failure("Unable to delete expense category.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete expense category: {ex.Message}");
        }
    }
}
