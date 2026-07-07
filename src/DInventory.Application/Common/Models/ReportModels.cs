namespace DInventory.Application.Common.Models;

public class SalesReportRow
{
    public DateTime SaleDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
}

public class StockReportRow
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal StockValue { get; set; }
    public bool IsLowStock { get; set; }

    /// <summary>Null if this variant has never sold. "Dead Stock" = no sale in DeadStockDays (see
    /// IsDeadStock) - a simple, cheap-to-compute stand-in for a full slow-moving-inventory report.</summary>
    public DateTime? LastSoldAt { get; set; }
    public bool IsDeadStock { get; set; }
}

public class ProductProfitRow
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Revenue - Cost;
}

public class CategorySalesRow
{
    public string CategoryName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class CustomerReportRow
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpend { get; set; }

    /// <summary>Rough estimate only (1 point per ৳100 of all-time spend) for a quick glance on
    /// this report - the real, persisted loyalty balance (earn/redeem/adjust ledger, configurable
    /// rate) lives on Customer.LoyaltyPointsBalance and the Customers module.</summary>
    public int LoyaltyPoints => (int)(TotalSpend / 100m);
}

public class ExpenseCategoryTotal
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

/// <summary>One row of the Gross Profit report - one calendar-month bucket within the requested
/// date range. GrossProfit = SalesAmount - PurchaseAmount - ExpenseAmount (the company's own
/// definition: total sales revenue for the period, less what was spent restocking and less
/// operating expenses - deliberately not COGS/per-product margin, which is what the separate
/// Product Profit report already covers).</summary>
public class GrossProfitRow
{
    public DateTime PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal SalesAmount { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal ExpenseAmount { get; set; }
    public decimal GrossProfit => SalesAmount - PurchaseAmount - ExpenseAmount;
}
