using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Expenses;

public interface IExpenseService
{
    /// <summary>The fixed category list backing the Expenses.Category CHECK constraint - kept as a
    /// static in-memory list rather than a manageable lookup table, per the "keep it simple" brief.</summary>
    static readonly string[] Categories =
    {
        "Shop Rent", "Electricity", "Salary", "Internet", "Packaging", "Marketing", "Courier", "Other"
    };

    Task<Expense?> GetByIdAsync(int expenseId);
    Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, string? category = null, int companyId = 0);
    Task<Result<int>> CreateAsync(Expense expense, int? actingUserId);
    Task<Result> UpdateAsync(Expense expense, int? actingUserId);
    Task<Result> DeleteAsync(int expenseId);
}
