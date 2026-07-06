using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Inventory;

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductVariantRepository _variantRepository;

    public StockService(IStockRepository stockRepository, IProductVariantRepository variantRepository)
    {
        _stockRepository = stockRepository;
        _variantRepository = variantRepository;
    }

    public Task<IEnumerable<Stock>> GetAllAsync(int companyId, bool onlyLowStock = false, string? search = null)
        => _stockRepository.GetAllAsync(companyId, onlyLowStock, search);

    public Task<PagedResult<Stock>> GetPagedAsync(PagedRequest request, int companyId, bool onlyLowStock = false)
        => _stockRepository.GetPagedAsync(request, companyId, onlyLowStock);

    public Task<Stock?> GetByVariantIdAsync(int productVariantId) => _stockRepository.GetByVariantIdAsync(productVariantId);

    public Task<IEnumerable<StockTransaction>> GetTransactionsAsync(int productVariantId)
        => _stockRepository.GetTransactionsForVariantAsync(productVariantId);

    public async Task<Result> AdjustStockByBarcodeAsync(string barcode, int quantity, string transactionType, string? remarks, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Result.Failure("Please scan or enter a barcode.");
        }

        var variant = await _variantRepository.GetByBarcodeAsync(barcode.Trim());
        if (variant is null)
        {
            return Result.Failure($"No product found for barcode '{barcode}'. Generate/print it first, or double-check the scan.");
        }

        return await AdjustStockAsync(variant.ProductVariantId, quantity, transactionType, remarks, actingUserId);
    }

    public async Task<Result> AdjustStockAsync(int productVariantId, int quantity, string transactionType, string? remarks, int? actingUserId)
    {
        var variant = await _variantRepository.GetByIdAsync(productVariantId);
        if (variant is null)
        {
            return Result.Failure("Product variant not found.");
        }

        if (quantity <= 0)
        {
            return Result.Failure("Quantity must be greater than zero.");
        }

        await _stockRepository.EnsureStockRowExistsAsync(productVariantId);

        var type = transactionType.Trim().ToUpperInvariant();
        int delta;
        switch (type)
        {
            case "IN":
                delta = quantity;
                break;
            case "OUT":
                var current = await _stockRepository.GetByVariantIdAsync(productVariantId);
                if (current is null || current.QuantityOnHand < quantity)
                {
                    return Result.Failure("Not enough stock on hand for this deduction.");
                }
                delta = -quantity;
                break;
            default:
                return Result.Failure("Unknown transaction type.");
        }

        await _stockRepository.AdjustQuantityAsync(productVariantId, delta);
        await _stockRepository.CreateTransactionAsync(new StockTransaction
        {
            ProductVariantId = productVariantId,
            TransactionType = type,
            Quantity = quantity,
            ReferenceType = "MANUAL",
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        });

        return Result.Success();
    }

    public Task<int> GetLowStockCountAsync() => _stockRepository.GetLowStockCountAsync();
}
