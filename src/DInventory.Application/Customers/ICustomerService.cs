using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Customers;

public interface ICustomerService
{
    Task<Customer?> GetByIdAsync(int customerId);

    /// <summary>Customer plus OrderCount/TotalSpend/LoyaltyPointsBalance - for the Details page.</summary>
    Task<Customer?> GetByIdWithStatsAsync(int customerId);

    Task<IEnumerable<Customer>> GetAllAsync(string? search = null);
    Task<PagedResult<Customer>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Customer customer, int? actingUserId);
    Task<Result> UpdateAsync(Customer customer, int? actingUserId);
    Task<Result> DeleteAsync(int customerId);
    Task<IEnumerable<SalesOrder>> GetSalesHistoryAsync(int customerId);
}
