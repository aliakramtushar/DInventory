using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Sales;

public class SalesReturnService : ISalesReturnService
{
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IStockRepository _stockRepository;

    public SalesReturnService(
        ISalesReturnRepository salesReturnRepository,
        ISalesOrderRepository salesOrderRepository,
        IStockRepository stockRepository)
    {
        _salesReturnRepository = salesReturnRepository;
        _salesOrderRepository = salesOrderRepository;
        _stockRepository = stockRepository;
    }

    public Task<SalesReturn?> GetByIdAsync(int salesReturnId) => _salesReturnRepository.GetByIdAsync(salesReturnId);

    public Task<PagedResult<SalesReturn>> GetPagedAsync(PagedRequest request, int? salesOrderId = null)
        => _salesReturnRepository.GetPagedAsync(request, salesOrderId);

    public async Task<Result<int>> CreateReturnAsync(CreateSalesReturnRequest request, int actingUserId)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<int>.Failure("Select at least one item and quantity to return.");
        }

        var order = await _salesOrderRepository.GetByIdAsync(request.SalesOrderId);
        if (order is null)
        {
            return Result<int>.Failure("Original sale not found.");
        }

        if (order.Status == "CANCELLED")
        {
            return Result<int>.Failure("This sale was already cancelled - its stock has already been restored, so it can't also be returned.");
        }

        var returnItems = new List<SalesReturnItem>();
        foreach (var requested in request.Items)
        {
            if (requested.Quantity <= 0)
            {
                return Result<int>.Failure("Return quantity must be greater than zero.");
            }

            var originalItem = order.Items.FirstOrDefault(i => i.SalesOrderItemId == requested.SalesOrderItemId);
            if (originalItem is null)
            {
                return Result<int>.Failure("One of the selected items does not belong to this invoice.");
            }

            var remaining = originalItem.Quantity - originalItem.ReturnedQuantity;
            if (requested.Quantity > remaining)
            {
                var label = $"{originalItem.ProductName} ({originalItem.SizeName})";
                return Result<int>.Failure($"Cannot return {requested.Quantity} of '{label}' - only {remaining} left returnable.");
            }

            // Effective per-unit price already accounts for whatever line discount was given at
            // sale time (LineTotal is net of that discount), so the refund is never overstated.
            var effectiveUnitPrice = originalItem.Quantity > 0
                ? Math.Round(originalItem.LineTotal / originalItem.Quantity, 2)
                : originalItem.UnitPrice;

            returnItems.Add(new SalesReturnItem
            {
                SalesOrderItemId = originalItem.SalesOrderItemId,
                ProductVariantId = originalItem.ProductVariantId,
                Quantity = requested.Quantity,
                UnitPrice = effectiveUnitPrice,
                LineTotal = requested.Quantity * effectiveUnitPrice
            });
        }

        var subTotal = returnItems.Sum(i => i.LineTotal);

        var salesReturn = new SalesReturn
        {
            ReturnNo = await _salesReturnRepository.GenerateNextReturnNoAsync(),
            SalesOrderId = order.SalesOrderId,
            ReturnDate = DateTime.UtcNow,
            SubTotal = subTotal,
            NetAmount = subTotal,
            Reason = request.Reason,
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId,
            Items = returnItems
        };

        var salesReturnId = await _salesReturnRepository.CreateAsync(salesReturn);

        // Return-to-stock: mirrors CancelSaleAsync's stock restoration, but per-line/partial
        // instead of the whole order, and tagged SALE_RETURN so it's distinguishable in history.
        foreach (var item in returnItems)
        {
            await _stockRepository.AdjustQuantityAsync(item.ProductVariantId, item.Quantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = item.ProductVariantId,
                TransactionType = "IN",
                Quantity = item.Quantity,
                ReferenceType = "SALE_RETURN",
                ReferenceId = salesReturnId,
                Remarks = $"Return {salesReturn.ReturnNo} against sale {order.InvoiceNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result<int>.Success(salesReturnId);
    }
}
