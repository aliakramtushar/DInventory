using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public interface ISupplierService
{
    Task<Supplier?> GetByIdAsync(int supplierId);
    Task<Supplier?> GetByIdWithDueAsync(int supplierId);
    Task<IEnumerable<Supplier>> GetAllAsync(int companyId = 0, bool onlyActive = false);
    Task<PagedResult<Supplier>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Supplier supplier, int? actingUserId);
    Task<Result> UpdateAsync(Supplier supplier, int? actingUserId);
    Task<Result> DeleteAsync(int supplierId);
    Task<IEnumerable<Purchase>> GetPurchaseHistoryAsync(int supplierId);
}
