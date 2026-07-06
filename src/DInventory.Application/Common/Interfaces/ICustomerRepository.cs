using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int customerId);

    /// <summary>Same customer, plus OrderCount/TotalSpend/LoyaltyPointsBalance joined in - for the
    /// Customer Details page and anywhere else that needs the full picture.</summary>
    Task<Customer?> GetByIdWithStatsAsync(int customerId);

    Task<IEnumerable<Customer>> GetAllAsync(string? search = null);
    Task<PagedResult<Customer>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<int> CreateAsync(Customer customer);
    Task<bool> UpdateAsync(Customer customer);
    Task<bool> DeleteAsync(int customerId);
    Task<bool> HasSalesAsync(int customerId);
    Task<int> GetTotalCountAsync(int companyId = 0);
}
