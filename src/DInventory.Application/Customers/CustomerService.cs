using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Customers;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;

    public CustomerService(ICustomerRepository customerRepository, ISalesOrderRepository salesOrderRepository)
    {
        _customerRepository = customerRepository;
        _salesOrderRepository = salesOrderRepository;
    }

    public Task<Customer?> GetByIdAsync(int customerId) => _customerRepository.GetByIdAsync(customerId);

    public Task<Customer?> GetByIdWithStatsAsync(int customerId) => _customerRepository.GetByIdWithStatsAsync(customerId);

    public Task<IEnumerable<Customer>> GetAllAsync(string? search = null) => _customerRepository.GetAllAsync(search);

    public Task<PagedResult<Customer>> GetPagedAsync(PagedRequest request, bool onlyActive = false)
        => _customerRepository.GetPagedAsync(request, onlyActive);

    public async Task<Result<int>> CreateAsync(Customer customer, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            return Result<int>.Failure("Customer name is required.");
        }

        if (!string.IsNullOrWhiteSpace(customer.Email) && !customer.Email.Contains('@'))
        {
            return Result<int>.Failure("Email address doesn't look valid.");
        }

        customer.CustomerName = customer.CustomerName.Trim();
        customer.CreatedBy = actingUserId;
        customer.CreatedAt = DateTime.UtcNow;
        customer.IsActive = true;
        var id = await _customerRepository.CreateAsync(customer);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(Customer customer, int? actingUserId)
    {
        var existing = await _customerRepository.GetByIdAsync(customer.CustomerId);
        if (existing is null)
        {
            return Result.Failure("Customer not found.");
        }

        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            return Result.Failure("Customer name is required.");
        }

        if (!string.IsNullOrWhiteSpace(customer.Email) && !customer.Email.Contains('@'))
        {
            return Result.Failure("Email address doesn't look valid.");
        }

        existing.CustomerName = customer.CustomerName.Trim();
        existing.Phone = customer.Phone;
        existing.Email = customer.Email;
        existing.Address = customer.Address;
        existing.IsActive = customer.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _customerRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update customer.");
    }

    public async Task<Result> DeleteAsync(int customerId)
    {
        if (await _customerRepository.HasSalesAsync(customerId))
        {
            return Result.Failure("Cannot delete a customer that already has sales history. Consider deactivating it instead.");
        }

        var ok = await _customerRepository.DeleteAsync(customerId);
        return ok ? Result.Success() : Result.Failure("Unable to delete customer.");
    }

    public Task<IEnumerable<SalesOrder>> GetSalesHistoryAsync(int customerId) => _salesOrderRepository.GetForCustomerAsync(customerId);
}
