using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(int purchaseId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns purchases across every company.</summary>
    Task<PagedResult<Purchase>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, int? supplierId = null);
    Task<IEnumerable<Purchase>> GetForSupplierAsync(int supplierId);
    Task<int> CreateAsync(Purchase purchase);
    Task<string> GenerateNextInvoiceNoAsync();
}
