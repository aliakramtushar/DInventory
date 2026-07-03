using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int customerId);
    Task<IEnumerable<Customer>> GetAllAsync(string? search = null);
    Task<int> CreateAsync(Customer customer);
    Task<int> GetTotalCountAsync();
}
