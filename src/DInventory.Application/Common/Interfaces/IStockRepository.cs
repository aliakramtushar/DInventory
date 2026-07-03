using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IStockRepository
{
    Task<Stock?> GetByVariantIdAsync(int productVariantId);
    Task<IEnumerable<Stock>> GetAllAsync(bool onlyLowStock = false, string? search = null);
    Task<PagedResult<Stock>> GetPagedAsync(PagedRequest request, bool onlyLowStock = false);
    Task EnsureStockRowExistsAsync(int productVariantId);
    Task<bool> AdjustQuantityAsync(int productVariantId, int deltaQuantity);
    Task<int> CreateTransactionAsync(StockTransaction transaction);
    Task<IEnumerable<StockTransaction>> GetTransactionsForVariantAsync(int productVariantId, int take = 50);
    Task<int> GetLowStockCountAsync();
    Task<int> GetTotalQuantityForProductAsync(int productId);

    /// <summary>Last time this variant appeared in an OUT/SALE stock transaction, or null if it has
    /// never sold - backs the Stock Report's simple "Dead Stock" flag.</summary>
    Task<DateTime?> GetLastSoldAtAsync(int productVariantId);
}
