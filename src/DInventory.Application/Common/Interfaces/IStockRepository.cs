using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IStockRepository
{
    Task<Stock?> GetByVariantIdAsync(int productVariantId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns stock across every company.</summary>
    Task<IEnumerable<Stock>> GetAllAsync(int companyId, bool onlyLowStock = false, string? search = null);
    /// <summary>maxStock filters to variants whose QuantityOnHand is at or below this threshold (null = no filter).</summary>
    Task<PagedResult<Stock>> GetPagedAsync(PagedRequest request, int companyId, int? maxStock = null);
    Task EnsureStockRowExistsAsync(int productVariantId);
    Task<bool> AdjustQuantityAsync(int productVariantId, int deltaQuantity);
    /// <summary>Atomically decrements stock only if there's enough on hand - the SQL WHERE clause
    /// makes the check-and-decrement a single indivisible operation, closing the race window that
    /// exists if you read quantity first and decrement separately. Returns false (no rows affected)
    /// if there wasn't enough stock, meaning the caller made no change at all.</summary>
    Task<bool> TryDecrementQuantityAsync(int productVariantId, int quantity);
    Task<int> CreateTransactionAsync(StockTransaction transaction);
    Task<IEnumerable<StockTransaction>> GetTransactionsForVariantAsync(int productVariantId, int take = 50);
    Task<int> GetLowStockCountAsync(int companyId = 0);
    Task<int> GetTotalQuantityForProductAsync(int productId);

    /// <summary>Last time this variant appeared in an OUT/SALE stock transaction, or null if it has
    /// never sold - backs the Stock Report's simple "Dead Stock" flag.</summary>
    Task<DateTime?> GetLastSoldAtAsync(int productVariantId);
}
