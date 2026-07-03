using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Sales;

public class SalesService : ISalesService
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;

    public SalesService(
        ISalesOrderRepository salesOrderRepository,
        IProductVariantRepository variantRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _variantRepository = variantRepository;
        _stockRepository = stockRepository;
        _customerRepository = customerRepository;
    }

    public Task<SalesOrder?> GetByIdAsync(int salesOrderId) => _salesOrderRepository.GetByIdAsync(salesOrderId);

    public Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null)
        => _salesOrderRepository.GetPagedAsync(request, fromDate, toDate);

    public Task<IEnumerable<Customer>> GetCustomersAsync(string? search = null) => _customerRepository.GetAllAsync(search);

    public async Task<Result<ProductVariant>> ScanForSaleAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Result<ProductVariant>.Failure("Please scan or enter a barcode.");
        }

        var variant = await _variantRepository.GetByBarcodeAsync(barcode.Trim());
        if (variant is null)
        {
            return Result<ProductVariant>.Failure($"No product found for barcode '{barcode}'.");
        }

        if (!variant.IsActive)
        {
            return Result<ProductVariant>.Failure($"'{variant.ProductName} ({variant.SizeName})' is deactivated and cannot be sold.");
        }

        if ((variant.QuantityOnHand ?? 0) <= 0)
        {
            return Result<ProductVariant>.Failure($"'{variant.ProductName} ({variant.SizeName})' is out of stock.");
        }

        return Result<ProductVariant>.Success(variant);
    }

    public async Task<Result<int>> CreateSaleAsync(CreateSaleRequest request, int actingUserId)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<int>.Failure("Please add at least one product to the sale.");
        }

        // Validate stock availability first
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return Result<int>.Failure("Item quantity must be greater than zero.");
            }

            var stock = await _stockRepository.GetByVariantIdAsync(item.ProductVariantId);
            if (stock is null || stock.QuantityOnHand < item.Quantity)
            {
                var variant = await _variantRepository.GetByIdAsync(item.ProductVariantId);
                var label = variant is null ? "product" : $"{variant.ProductName} ({variant.SizeName})";
                return Result<int>.Failure($"Insufficient stock for '{label}'. Available: {stock?.QuantityOnHand ?? 0}.");
            }
        }

        int? customerId = request.CustomerId;
        if (customerId is null && !string.IsNullOrWhiteSpace(request.NewCustomerName))
        {
            customerId = await _customerRepository.CreateAsync(new Customer
            {
                CustomerName = request.NewCustomerName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ---- Discount math -------------------------------------------------------------------
        // Each line can carry its own PERCENT-or-FIXED discount; the bill can additionally carry
        // one more PERCENT-or-FIXED discount on top of the post-line-discount subtotal. SubTotal
        // stays the GROSS (pre-discount) sum of Quantity*UnitPrice and DiscountAmount becomes the
        // single combined (line + bill) money figure, exactly like before this feature existed -
        // so NetAmount = SubTotal - DiscountAmount + TaxAmount still holds and every existing
        // report/query that only reads SubTotal/DiscountAmount/NetAmount keeps working unchanged.
        var grossSubTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var items = new List<SalesOrderItem>();
        decimal lineDiscountTotal = 0;
        foreach (var i in request.Items)
        {
            var lineGross = i.Quantity * i.UnitPrice;
            var lineDiscount = ComputeDiscountAmount(lineGross, i.DiscountType, i.DiscountValue);
            lineDiscountTotal += lineDiscount;

            items.Add(new SalesOrderItem
            {
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountType = NormalizeDiscountType(i.DiscountType),
                DiscountValue = i.DiscountValue < 0 ? 0 : i.DiscountValue,
                DiscountAmount = lineDiscount,
                LineTotal = lineGross - lineDiscount
            });
        }

        var postLineSubTotal = grossSubTotal - lineDiscountTotal;
        var billDiscount = ComputeDiscountAmount(postLineSubTotal, request.DiscountType, request.DiscountValue);
        var totalDiscountAmount = lineDiscountTotal + billDiscount;

        var netAmount = grossSubTotal - totalDiscountAmount + request.TaxAmount;
        if (netAmount < 0) netAmount = 0;

        var order = new SalesOrder
        {
            InvoiceNo = await _salesOrderRepository.GenerateNextInvoiceNoAsync(),
            CustomerId = customerId,
            SaleDate = DateTime.UtcNow,
            SubTotal = grossSubTotal,
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue < 0 ? 0 : request.DiscountValue,
            DiscountAmount = totalDiscountAmount,
            TaxAmount = request.TaxAmount,
            NetAmount = netAmount,
            PaymentStatus = string.IsNullOrWhiteSpace(request.PaymentStatus) ? "PAID" : request.PaymentStatus,
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod,
            Status = "COMPLETED",
            Remarks = request.Remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId,
            Items = items
        };

        var salesOrderId = await _salesOrderRepository.CreateAsync(order);

        foreach (var item in order.Items)
        {
            await _stockRepository.AdjustQuantityAsync(item.ProductVariantId, -item.Quantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = item.ProductVariantId,
                TransactionType = "OUT",
                Quantity = item.Quantity,
                ReferenceType = "SALE",
                ReferenceId = salesOrderId,
                Remarks = $"Sale {order.InvoiceNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result<int>.Success(salesOrderId);
    }

    private static string NormalizeDiscountType(string? discountType)
        => string.Equals(discountType, "PERCENT", StringComparison.OrdinalIgnoreCase) ? "PERCENT" : "FIXED";

    /// <summary>PERCENT is a percentage of <paramref name="baseAmount"/>; FIXED is a flat amount.
    /// Either way the result is clamped to [0, baseAmount] so a discount can never make a line or
    /// the bill go negative.</summary>
    private static decimal ComputeDiscountAmount(decimal baseAmount, string? discountType, decimal discountValue)
    {
        if (baseAmount <= 0 || discountValue <= 0)
        {
            return 0;
        }

        var amount = string.Equals(discountType, "PERCENT", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(baseAmount * discountValue / 100m, 2)
            : discountValue;

        if (amount < 0) amount = 0;
        if (amount > baseAmount) amount = baseAmount;
        return amount;
    }

    public async Task<Result> CancelSaleAsync(int salesOrderId, int actingUserId)
    {
        var order = await _salesOrderRepository.GetByIdAsync(salesOrderId);
        if (order is null)
        {
            return Result.Failure("Sales order not found.");
        }

        if (order.Status == "CANCELLED")
        {
            return Result.Failure("This sale is already cancelled.");
        }

        // Return stock
        foreach (var item in order.Items)
        {
            await _stockRepository.AdjustQuantityAsync(item.ProductVariantId, item.Quantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = item.ProductVariantId,
                TransactionType = "IN",
                Quantity = item.Quantity,
                ReferenceType = "SALE",
                ReferenceId = salesOrderId,
                Remarks = $"Cancellation of sale {order.InvoiceNo}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        var ok = await _salesOrderRepository.CancelAsync(salesOrderId);
        return ok ? Result.Success() : Result.Failure("Unable to cancel this sale.");
    }
}
