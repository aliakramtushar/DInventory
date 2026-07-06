using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public class PurchaseReturnService : IPurchaseReturnService
{
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IStockRepository _stockRepository;

    public PurchaseReturnService(
        IPurchaseReturnRepository purchaseReturnRepository,
        IPurchaseRepository purchaseRepository,
        IStockRepository stockRepository)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
        _purchaseRepository = purchaseRepository;
        _stockRepository = stockRepository;
    }

    public Task<PurchaseReturn?> GetByIdAsync(int purchaseReturnId) => _purchaseReturnRepository.GetByIdAsync(purchaseReturnId);

    public Task<PagedResult<PurchaseReturn>> GetPagedAsync(PagedRequest request, int companyId, int? purchaseId = null)
        => _purchaseReturnRepository.GetPagedAsync(request, companyId, purchaseId);

    public async Task<Result<int>> CreateReturnAsync(CreatePurchaseReturnRequest request, int actingUserId)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<int>.Failure("Select at least one item and quantity to return.");
        }

        var purchase = await _purchaseRepository.GetByIdAsync(request.PurchaseId);
        if (purchase is null)
        {
            return Result<int>.Failure("Original purchase not found.");
        }

        var returnItems = new List<PurchaseReturnItem>();
        foreach (var requested in request.Items)
        {
            if (requested.Quantity <= 0)
            {
                return Result<int>.Failure("Return quantity must be greater than zero.");
            }

            var originalItem = purchase.Items.FirstOrDefault(i => i.PurchaseItemId == requested.PurchaseItemId);
            if (originalItem is null)
            {
                return Result<int>.Failure("One of the selected items does not belong to this purchase.");
            }

            var remaining = originalItem.Quantity - originalItem.ReturnedQuantity;
            if (requested.Quantity > remaining)
            {
                var label = $"{originalItem.ProductName} ({originalItem.SizeName})";
                return Result<int>.Failure($"Cannot return {requested.Quantity} of '{label}' - only {remaining} left returnable.");
            }

            var stock = await _stockRepository.GetByVariantIdAsync(originalItem.ProductVariantId);
            if (stock is null || stock.QuantityOnHand < requested.Quantity)
            {
                var label = $"{originalItem.ProductName} ({originalItem.SizeName})";
                return Result<int>.Failure($"Cannot return {requested.Quantity} of '{label}' - only {stock?.QuantityOnHand ?? 0} currently in stock.");
            }

            returnItems.Add(new PurchaseReturnItem
            {
                PurchaseItemId = originalItem.PurchaseItemId,
                ProductVariantId = originalItem.ProductVariantId,
                Quantity = requested.Quantity,
                UnitPrice = originalItem.BuyingPrice,
                LineTotal = requested.Quantity * originalItem.BuyingPrice
            });
        }

        var totalAmount = returnItems.Sum(i => i.LineTotal);

        var purchaseReturn = new PurchaseReturn
        {
            ReturnNo = await _purchaseReturnRepository.GenerateNextReturnNoAsync(),
            PurchaseId = purchase.PurchaseId,
            CompanyId = purchase.CompanyId,
            BusinessUnitId = purchase.BusinessUnitId,
            ReturnDate = DateTime.UtcNow,
            TotalAmount = totalAmount,
            Reason = request.Reason,
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId,
            Items = returnItems
        };

        var purchaseReturnId = await _purchaseReturnRepository.CreateAsync(purchaseReturn);

        // Sending stock back to the supplier reduces stock the same way a sale does, just tagged
        // PURCHASE_RETURN so it's distinguishable from a SALE in stock history.
        foreach (var item in returnItems)
        {
            await _stockRepository.AdjustQuantityAsync(item.ProductVariantId, -item.Quantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = item.ProductVariantId,
                TransactionType = "OUT",
                Quantity = item.Quantity,
                ReferenceType = "PURCHASE_RETURN",
                ReferenceId = purchaseReturnId,
                Remarks = $"Return {purchaseReturn.ReturnNo} against purchase {purchase.PurchaseInvoiceNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result<int>.Success(purchaseReturnId);
    }
}
