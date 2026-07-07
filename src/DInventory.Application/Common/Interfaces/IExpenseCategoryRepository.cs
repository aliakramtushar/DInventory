using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IExpenseCategoryRepository
{
    Task<ExpenseCategory?> GetByIdAsync(int expenseCategoryId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns categories across every company.</summary>
    Task<IEnumerable<ExpenseCategory>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<ExpenseCategory>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<int> CreateAsync(ExpenseCategory expenseCategory);
    Task<bool> UpdateAsync(ExpenseCategory expenseCategory);
    Task<bool> DeleteAsync(int expenseCategoryId);
    Task<bool> HasExpensesAsync(int expenseCategoryId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
}
