using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public interface IExpenseSubcategoryService
{
    Task<ExpenseSubcategory?> GetByIdAsync(int expenseSubcategoryId);
    Task<IEnumerable<ExpenseSubcategory>> GetAllAsync(int companyId, int? expenseCategoryId = null, string? search = null, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<ExpenseSubcategory>> GetPagedAsync(PagedRequest request, int companyId, int? expenseCategoryId = null, bool onlyActive = false, int? businessUnitId = null);
    Task<Result<int>> CreateAsync(ExpenseSubcategory expenseSubcategory, int? actingUserId);
    Task<Result> UpdateAsync(ExpenseSubcategory expenseSubcategory, int? actingUserId);
    Task<Result> DeleteAsync(int expenseSubcategoryId);
}
