using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public class PurchaseService : IPurchaseService
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly IStockRepository _stockRepository;

    public PurchaseService(
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository,
        IProductVariantRepository variantRepository,
        IStockRepository stockRepository)
    {
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
        _variantRepository = variantRepository;
        _stockRepository = stockRepository;
    }

    public Task<Purchase?> GetByIdAsync(int purchaseId) => _purchaseRepository.GetByIdAsync(purchaseId);

    public Task<PagedResult<Purchase>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, int? supplierId = null)
        => _purchaseRepository.GetPagedAsync(request, companyId, businessUnitId, supplierId);

    public async Task<Result<int>> CreateAsync(CreatePurchaseRequest request, int actingUserId, int companyId, int? businessUnitId)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<int>.Failure("Add at least one product/size to the purchase.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId);
        if (supplier is null)
        {
            return Result<int>.Failure("Supplier not found.");
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return Result<int>.Failure("Every line item's quantity must be greater than zero.");
            }

            var variant = await _variantRepository.GetByIdAsync(item.ProductVariantId);
            if (variant is null)
            {
                return Result<int>.Failure("One of the selected products/sizes no longer exists.");
            }
        }

        var totalAmount = request.Items.Sum(i => i.Quantity * i.BuyingPrice);
        if (request.PaidAmount < 0 || request.PaidAmount > totalAmount)
        {
            return Result<int>.Failure("Paid amount must be between 0 and the purchase total.");
        }

        var purchase = new Purchase
        {
            PurchaseInvoiceNo = await _purchaseRepository.GenerateNextInvoiceNoAsync(),
            SupplierId = request.SupplierId,
            PurchaseDate = DateTime.UtcNow,
            TotalAmount = totalAmount,
            PaidAmount = request.PaidAmount,
            Remarks = request.Remarks,
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId,
            Items = request.Items.Select(i => new PurchaseItem
            {
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                BuyingPrice = i.BuyingPrice,
                LineTotal = i.Quantity * i.BuyingPrice
            }).ToList()
        };

        var purchaseId = await _purchaseRepository.CreateAsync(purchase);

        // Auto stock update: receiving a purchase increases stock the same way a sale decreases it.
        foreach (var item in purchase.Items)
        {
            await _stockRepository.EnsureStockRowExistsAsync(item.ProductVariantId);
            await _stockRepository.AdjustQuantityAsync(item.ProductVariantId, item.Quantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = item.ProductVariantId,
                TransactionType = "IN",
                Quantity = item.Quantity,
                ReferenceType = "PURCHASE",
                ReferenceId = purchaseId,
                Remarks = $"Purchase {purchase.PurchaseInvoiceNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result<int>.Success(purchaseId);
    }
}
