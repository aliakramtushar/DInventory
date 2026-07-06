using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Inventory;

public interface IStockService
{
    Task<IEnumerable<Stock>> GetAllAsync(int companyId, bool onlyLowStock = false, string? search = null);
    Task<PagedResult<Stock>> GetPagedAsync(PagedRequest request, int companyId, bool onlyLowStock = false);
    Task<Stock?> GetByVariantIdAsync(int productVariantId);
    Task<IEnumerable<StockTransaction>> GetTransactionsAsync(int productVariantId);

    /// <summary>Adjusts stock for a variant found by barcode - what the barcode scanner calls during stock entry.</summary>
    Task<Result> AdjustStockByBarcodeAsync(string barcode, int quantity, string transactionType, string? remarks, int? actingUserId);

    Task<Result> AdjustStockAsync(int productVariantId, int quantity, string transactionType, string? remarks, int? actingUserId);
    Task<int> GetLowStockCountAsync();
}
