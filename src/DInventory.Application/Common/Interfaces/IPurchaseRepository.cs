using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(int purchaseId);
    Task<PagedResult<Purchase>> GetPagedAsync(PagedRequest request, int? supplierId = null);
    Task<IEnumerable<Purchase>> GetForSupplierAsync(int supplierId);
    Task<int> CreateAsync(Purchase purchase);
    Task<string> GenerateNextInvoiceNoAsync();
}
