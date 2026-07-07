using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Customers;
using DInventory.Domain.Entities;

namespace DInventory.Application.Sales;

public class SalesService : ISalesService
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILoyaltyService _loyaltyService;

    public SalesService(
        ISalesOrderRepository salesOrderRepository,
        IProductVariantRepository variantRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository,
        ILoyaltyService loyaltyService)
    {
        _salesOrderRepository = salesOrderRepository;
        _variantRepository = variantRepository;
        _stockRepository = stockRepository;
        _customerRepository = customerRepository;
        _loyaltyService = loyaltyService;
    }

    public Task<SalesOrder?> GetByIdAsync(int salesOrderId) => _salesOrderRepository.GetByIdAsync(salesOrderId);

    public Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, DateTime? fromDate = null, DateTime? toDate = null)
        => _salesOrderRepository.GetPagedAsync(request, companyId, businessUnitId, fromDate, toDate);

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

    public async Task<Result<int>> CreateSaleAsync(CreateSaleRequest request, int actingUserId, int companyId, int? businessUnitId)
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
                Phone = string.IsNullOrWhiteSpace(request.NewCustomerMobile) ? null : request.NewCustomerMobile.Trim(),
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

        // ---- Loyalty: redeem points against this bill, then earn points on what's actually paid ----
        // Only a resolved, existing customer can redeem (a brand-new quick-add customer always has
        // a zero balance anyway, so this naturally does nothing for them). Redeeming reduces
        // NetAmount further, on top of any line/bill discount already applied; earning is
        // calculated on the final, post-redeem NetAmount so points are never awarded on money the
        // customer didn't actually pay.
        var loyaltySettings = await _loyaltyService.GetSettingsAsync();
        var redeemPoints = 0;
        var redeemAmount = 0m;
        if (customerId.HasValue && request.RedeemPoints > 0)
        {
            if (!loyaltySettings.IsEnabled || loyaltySettings.PointValueOnRedeem <= 0)
            {
                return Result<int>.Failure("Loyalty point redemption isn't enabled.");
            }

            var balance = await _loyaltyService.GetBalanceAsync(customerId.Value);
            if (request.RedeemPoints > balance)
            {
                return Result<int>.Failure($"This customer only has {balance} loyalty point(s) available.");
            }

            var wouldBeAmount = _loyaltyService.ComputeRedeemValue(request.RedeemPoints, loyaltySettings);
            if (wouldBeAmount > netAmount)
            {
                return Result<int>.Failure("Cannot redeem that many points - their value is more than this bill's total.");
            }

            redeemPoints = request.RedeemPoints;
            redeemAmount = wouldBeAmount;
            netAmount -= redeemAmount;
        }

        var pointsEarned = customerId.HasValue ? _loyaltyService.ComputeEarnedPoints(netAmount, loyaltySettings) : 0;

        var order = new SalesOrder
        {
            InvoiceNo = await _salesOrderRepository.GenerateNextInvoiceNoAsync(),
            CustomerId = customerId,
            SaleDate = DateTime.UtcNow,
            SubTotal = grossSubTotal,
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue < 0 ? 0 : request.DiscountValue,
            DiscountAmount = totalDiscountAmount + redeemAmount,
            TaxAmount = request.TaxAmount,
            NetAmount = netAmount,
            LoyaltyPointsEarned = pointsEarned,
            LoyaltyPointsRedeemed = redeemPoints,
            LoyaltyRedeemAmount = redeemAmount,
            PaymentStatus = string.IsNullOrWhiteSpace(request.PaymentStatus) ? "PAID" : request.PaymentStatus,
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod,
            Status = "COMPLETED",
            Remarks = request.Remarks,
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId,
            Items = items
        };

        try
        {
            // Atomically decrement stock for every item in a single first pass, before the order
            // (or anything else) is written. TryDecrementQuantityAsync's WHERE clause makes each
            // item's check-and-decrement one indivisible DB operation, closing the race window that
            // the up-front read-based check above can't fully close on its own (two concurrent sales
            // can both pass that read before either has decremented). We only move on to creating the
            // order/transactions/loyalty entries if every single item's decrement succeeds; if one
            // loses the race, we give back whichever earlier items in this same pass already
            // succeeded (nothing else has been written yet, so there's nothing else to undo).
            var decrementedSoFar = new List<SalesOrderItem>();
            foreach (var item in order.Items)
            {
                var decremented = await _stockRepository.TryDecrementQuantityAsync(item.ProductVariantId, item.Quantity);
                if (!decremented)
                {
                    foreach (var undo in decrementedSoFar)
                    {
                        await _stockRepository.AdjustQuantityAsync(undo.ProductVariantId, undo.Quantity);
                    }

                    return Result<int>.Failure("Insufficient stock for one of the items in this sale (someone else may have just sold the last of it) - please refresh and try again.");
                }

                decrementedSoFar.Add(item);
            }

            var salesOrderId = await _salesOrderRepository.CreateAsync(order);

            foreach (var item in order.Items)
            {
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

            if (customerId.HasValue && redeemPoints > 0)
            {
                await _loyaltyService.RedeemAsync(customerId.Value, redeemPoints, "SALE", salesOrderId, $"Redeemed on sale {order.InvoiceNo}", actingUserId);
            }

            if (customerId.HasValue && pointsEarned > 0)
            {
                await _loyaltyService.EarnAsync(customerId.Value, pointsEarned, "SALE", salesOrderId, $"Earned from sale {order.InvoiceNo}", actingUserId);
            }

            return Result<int>.Success(salesOrderId);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save sale: {ex.Message}");
        }
    }

    // Default is PERCENT: only an explicit "FIXED" is treated as a flat amount, so a missing/
    // unrecognized value (e.g. an old client that didn't send one) falls back to the new default
    // rather than silently becoming a flat-amount discount.
    private static string NormalizeDiscountType(string? discountType)
        => string.Equals(discountType, "FIXED", StringComparison.OrdinalIgnoreCase) ? "FIXED" : "PERCENT";

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

        try
        {
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

            // Reverse whatever loyalty effect this sale had: take back any points it earned, and
            // hand back any points it redeemed. Uses enforceBalanceFloor: false because this is a
            // system-initiated reversal, not a staff-entered adjustment - it must go through even if
            // the customer's balance has moved since (e.g. they've already redeemed the earned points
            // elsewhere), same reasoning as CancelSaleAsync unconditionally restoring stock above.
            if (order.CustomerId.HasValue)
            {
                if (order.LoyaltyPointsEarned > 0)
                {
                    await _loyaltyService.AdjustAsync(order.CustomerId.Value, -order.LoyaltyPointsEarned,
                        $"Reversal: cancelled sale {order.InvoiceNo}", actingUserId, enforceBalanceFloor: false);
                }

                if (order.LoyaltyPointsRedeemed > 0)
                {
                    await _loyaltyService.AdjustAsync(order.CustomerId.Value, order.LoyaltyPointsRedeemed,
                        $"Refund: cancelled sale {order.InvoiceNo}", actingUserId, enforceBalanceFloor: false);
                }
            }

            var ok = await _salesOrderRepository.CancelAsync(salesOrderId);
            return ok ? Result.Success() : Result.Failure("Unable to cancel this sale.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to cancel sale: {ex.Message}");
        }
    }
}
