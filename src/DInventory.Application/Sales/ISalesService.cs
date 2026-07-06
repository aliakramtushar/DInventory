using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Sales;

public interface ISalesService
{
    Task<SalesOrder?> GetByIdAsync(int salesOrderId);
    Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<Result<int>> CreateSaleAsync(CreateSaleRequest request, int actingUserId, int companyId, int? businessUnitId);
    Task<Result> CancelSaleAsync(int salesOrderId, int actingUserId);
    Task<IEnumerable<Customer>> GetCustomersAsync(string? search = null);

    /// <summary>Scan-to-cart: resolves a barcode into a sellable line (name, size, price, stock on hand) for the POS screen.</summary>
    Task<Result<ProductVariant>> ScanForSaleAsync(string barcode);
}
