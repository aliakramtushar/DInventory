using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(int expenseId);
    /// <summary>companyId = 0 (superuser/"All Companies") bypasses the filter and returns expenses
    /// across every company. Defaults to 0 so existing call sites (e.g. ReportService, which does not
    /// yet scope by company) keep compiling unchanged.</summary>
    Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, string? category = null, int companyId = 0);
    Task<int> CreateAsync(Expense expense);
    Task<bool> UpdateAsync(Expense expense);
    Task<bool> DeleteAsync(int expenseId);
    Task<decimal> GetTotalAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);
    Task<IEnumerable<ExpenseCategoryTotal>> GetSummaryByCategoryAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0);
}
