using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(int expenseId);
    Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, string? category = null);
    Task<int> CreateAsync(Expense expense);
    Task<bool> UpdateAsync(Expense expense);
    Task<bool> DeleteAsync(int expenseId);
    Task<decimal> GetTotalAsync(DateTime fromDate, DateTime toDateExclusive);
    Task<IEnumerable<ExpenseCategoryTotal>> GetSummaryByCategoryAsync(DateTime fromDate, DateTime toDateExclusive);
}
