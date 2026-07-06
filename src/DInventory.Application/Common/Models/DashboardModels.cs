namespace DInventory.Application.Common.Models;

public class SalesSummaryPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int OrderCount { get; set; }
}

public class DashboardStats
{
    public decimal TodaySales { get; set; }
    public decimal WeekSales { get; set; }
    public decimal MonthSales { get; set; }
    public decimal YearSales { get; set; }
    public int TodayOrderCount { get; set; }
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int TotalCustomers { get; set; }

    /// <summary>Revenue minus cost-of-goods-sold for today's completed sales.</summary>
    public decimal TodayProfit { get; set; }

    /// <summary>Simplified on purpose: today's CASH-method sales minus today's total expenses
    /// (assumes expenses are paid out of the cash drawer - a reasonable approximation for a small
    /// shop's "how much cash do I actually have right now" check, not full accounting reconciliation).</summary>
    public decimal CashInHand { get; set; }

    /// <summary>The range the four stats below are computed over - one of "today", "7day",
    /// "1month", "1year" - driven by the dashboard's range filter dropdown.</summary>
    public string Range { get; set; } = "today";
    public decimal RangeSales { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProductSold { get; set; }
    public decimal RangeProfit { get; set; }

    /// <summary>Percent change of RangeSales vs. the immediately preceding period of the same
    /// length (e.g. this week vs last week). Null when there's no prior-period sales to compare
    /// against (division by zero would otherwise be meaningless).</summary>
    public decimal? RangeGrowthPercent { get; set; }

    /// <summary>Last 6 calendar months of completed sales revenue, oldest first - feeds the
    /// "Monthly Sales Growth" bar chart.</summary>
    public List<SalesSummaryPoint> MonthlyTrend { get; set; } = new();

    public List<SalesSummaryPoint> Trend { get; set; } = new();
    public List<TopProduct> TopProducts { get; set; } = new();
}

public class TopProduct
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}
