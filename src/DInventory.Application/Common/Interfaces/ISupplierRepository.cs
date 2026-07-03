using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(int supplierId);
    Task<Supplier?> GetByIdWithDueAsync(int supplierId);
    Task<IEnumerable<Supplier>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Supplier>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<int> CreateAsync(Supplier supplier);
    Task<bool> UpdateAsync(Supplier supplier);
    Task<bool> DeleteAsync(int supplierId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> HasPurchasesAsync(int supplierId);
}
