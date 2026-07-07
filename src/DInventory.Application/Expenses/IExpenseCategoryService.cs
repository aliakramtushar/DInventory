using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public interface IExpenseCategoryService
{
    Task<ExpenseCategory?> GetByIdAsync(int expenseCategoryId);
    Task<IEnumerable<ExpenseCategory>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<ExpenseCategory>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<Result<int>> CreateAsync(ExpenseCategory expenseCategory, int? actingUserId);
    Task<Result> UpdateAsync(ExpenseCategory expenseCategory, int? actingUserId);
    Task<Result> DeleteAsync(int expenseCategoryId);
}
