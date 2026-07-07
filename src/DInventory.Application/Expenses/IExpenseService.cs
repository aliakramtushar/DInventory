using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public interface IExpenseService
{
    Task<Expense?> GetByIdAsync(int expenseId);
    Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, int? expenseCategoryId = null, int companyId = 0);
    Task<Result<int>> CreateAsync(Expense expense, int? actingUserId);
    Task<Result> UpdateAsync(Expense expense, int? actingUserId);
    Task<Result> DeleteAsync(int expenseId);
}
