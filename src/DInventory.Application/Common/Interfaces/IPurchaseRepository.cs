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

    /// <summary>Sum of (TotalAmount - Discount) for purchases received within the date range - the
    /// "Purchase Amount" the dashboard's Gross/Net Profit figures are computed against.</summary>
    Task<decimal> GetTotalAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);
}
