using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public class ExpenseSubcategoryService : IExpenseSubcategoryService
{
    private readonly IExpenseSubcategoryRepository _expenseSubcategoryRepository;

    public ExpenseSubcategoryService(IExpenseSubcategoryRepository expenseSubcategoryRepository)
    {
        _expenseSubcategoryRepository = expenseSubcategoryRepository;
    }

    public Task<ExpenseSubcategory?> GetByIdAsync(int expenseSubcategoryId) => _expenseSubcategoryRepository.GetByIdAsync(expenseSubcategoryId);

    public Task<IEnumerable<ExpenseSubcategory>> GetAllAsync(int companyId, int? expenseCategoryId = null, string? search = null, bool onlyActive = false, int? businessUnitId = null)
        => _expenseSubcategoryRepository.GetAllAsync(companyId, expenseCategoryId, search, onlyActive, businessUnitId);

    public Task<PagedResult<ExpenseSubcategory>> GetPagedAsync(PagedRequest request, int companyId, int? expenseCategoryId = null, bool onlyActive = false, int? businessUnitId = null)
        => _expenseSubcategoryRepository.GetPagedAsync(request, companyId, expenseCategoryId, onlyActive, businessUnitId);

    public async Task<Result<int>> CreateAsync(ExpenseSubcategory expenseSubcategory, int? actingUserId)
    {
        expenseSubcategory.CreatedBy = actingUserId;
        expenseSubcategory.CreatedAt = DateTime.UtcNow;
        expenseSubcategory.IsActive = true;

        try
        {
            var id = await _expenseSubcategoryRepository.CreateAsync(expenseSubcategory);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save expense subcategory: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(ExpenseSubcategory expenseSubcategory, int? actingUserId)
    {
        var existing = await _expenseSubcategoryRepository.GetByIdAsync(expenseSubcategory.ExpenseSubcategoryId);
        if (existing is null)
        {
            return Result.Failure("Expense subcategory not found.");
        }

        existing.ExpenseCategoryId = expenseSubcategory.ExpenseCategoryId;
        existing.ExpenseSubcategoryName = expenseSubcategory.ExpenseSubcategoryName;
        existing.Description = expenseSubcategory.Description;
        existing.IsActive = expenseSubcategory.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _expenseSubcategoryRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update expense subcategory.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update expense subcategory: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int expenseSubcategoryId)
    {
        if (await _expenseSubcategoryRepository.HasExpensesAsync(expenseSubcategoryId))
        {
            return Result.Failure("Cannot delete an expense subcategory that still has expenses recorded against it.");
        }

        try
        {
            var ok = await _expenseSubcategoryRepository.DeleteAsync(expenseSubcategoryId);
            return ok ? Result.Success() : Result.Failure("Unable to delete expense subcategory.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete expense subcategory: {ex.Message}");
        }
    }
}
