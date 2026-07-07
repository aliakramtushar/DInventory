using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IExpenseSubcategoryRepository
{
    Task<ExpenseSubcategory?> GetByIdAsync(int expenseSubcategoryId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns subcategories across every company.</summary>
    Task<IEnumerable<ExpenseSubcategory>> GetAllAsync(int companyId, int? expenseCategoryId = null, string? search = null, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<ExpenseSubcategory>> GetPagedAsync(PagedRequest request, int companyId, int? expenseCategoryId = null, bool onlyActive = false, int? businessUnitId = null);
    Task<int> CreateAsync(ExpenseSubcategory expenseSubcategory);
    Task<bool> UpdateAsync(ExpenseSubcategory expenseSubcategory);
    Task<bool> DeleteAsync(int expenseSubcategoryId);
    Task<bool> HasExpensesAsync(int expenseSubcategoryId);
}
