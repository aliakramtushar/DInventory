using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Expenses;

namespace DInventory.Application.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IExpenseRepository _expenseRepository;

    public DashboardService(
        ISalesOrderRepository salesOrderRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository,
        IExpenseRepository expenseRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _customerRepository = customerRepository;
        _expenseRepository = expenseRepository;
    }

    public async Task<DashboardStats> GetStatsAsync(int companyId = 0, int? businessUnitId = null, string range = "today")
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var yearStart = new DateTime(now.Year, 1, 1);
        var tomorrow = todayStart.AddDays(1);

        var normalizedRange = string.IsNullOrWhiteSpace(range) ? "today" : range.ToLowerInvariant();
        var (rangeFrom, rangeTo) = ResolveRange(normalizedRange, todayStart, tomorrow);

        var rangeSpan = rangeTo - rangeFrom;
        var previousFrom = rangeFrom - rangeSpan;
        var previousTo = rangeFrom;

        var todayCashSales = await _salesOrderRepository.GetSalesTotalByMethodAsync(todayStart, tomorrow, "CASH", companyId, businessUnitId);
        var todayExpenses = await _expenseRepository.GetTotalAsync(todayStart, tomorrow, companyId, businessUnitId);

        var rangeSales = await _salesOrderRepository.GetSalesTotalAsync(rangeFrom, rangeTo, companyId, businessUnitId);
        var previousRangeSales = await _salesOrderRepository.GetSalesTotalAsync(previousFrom, previousTo, companyId, businessUnitId);

        var sixMonthsAgoStart = new DateTime(now.Year, now.Month, 1).AddMonths(-5);

        var stats = new DashboardStats();
        stats.TodaySales = await _salesOrderRepository.GetSalesTotalAsync(todayStart, tomorrow, companyId, businessUnitId);
        stats.WeekSales = await _salesOrderRepository.GetSalesTotalAsync(weekStart, tomorrow, companyId, businessUnitId);
        stats.MonthSales = await _salesOrderRepository.GetSalesTotalAsync(monthStart, tomorrow, companyId, businessUnitId);
        stats.YearSales = await _salesOrderRepository.GetSalesTotalAsync(yearStart, tomorrow, companyId, businessUnitId);
        stats.TodayOrderCount = await _salesOrderRepository.GetOrderCountAsync(todayStart, tomorrow, companyId, businessUnitId);
        stats.TotalProducts = await _productRepository.GetTotalActiveCountAsync(companyId);
        stats.LowStockCount = await _stockRepository.GetLowStockCountAsync(companyId);
        stats.TotalCustomers = await _customerRepository.GetTotalCountAsync(companyId);
        stats.TodayProfit = await _salesOrderRepository.GetProfitTotalAsync(todayStart, tomorrow, companyId, businessUnitId);
        stats.CashInHand = todayCashSales - todayExpenses;

        stats.Range = normalizedRange;
        stats.RangeSales = rangeSales;
        stats.TotalOrders = await _salesOrderRepository.GetOrderCountAsync(rangeFrom, rangeTo, companyId, businessUnitId);
        stats.TotalProductSold = await _salesOrderRepository.GetTotalQuantitySoldAsync(rangeFrom, rangeTo, companyId, businessUnitId);
        stats.RangeProfit = await _salesOrderRepository.GetProfitTotalAsync(rangeFrom, rangeTo, companyId, businessUnitId);
        stats.RangeGrowthPercent = previousRangeSales > 0
            ? Math.Round(((rangeSales - previousRangeSales) / previousRangeSales) * 100m, 1)
            : (decimal?)null;

        stats.Trend = (await _salesOrderRepository.GetSalesTrendAsync(todayStart.AddDays(-13), tomorrow, TrendGranularity.Day, companyId, businessUnitId)).ToList();
        stats.MonthlyTrend = (await _salesOrderRepository.GetSalesTrendAsync(sixMonthsAgoStart, tomorrow, TrendGranularity.Month, companyId, businessUnitId)).ToList();
        stats.TopProducts = (await _salesOrderRepository.GetTopProductsAsync(monthStart, tomorrow, 5, companyId, businessUnitId)).ToList();

        return stats;
    }

    public async Task<IEnumerable<SalesSummaryPoint>> GetTrendAsync(string range, int companyId = 0, int? businessUnitId = null)
    {
        var now = DateTime.UtcNow;
        var tomorrow = now.Date.AddDays(1);
        var normalized = range.ToLowerInvariant();

        if (normalized == "weekly")
        {
            return await _salesOrderRepository.GetSalesTrendAsync(now.Date.AddDays(-77), tomorrow, TrendGranularity.Week, companyId, businessUnitId);
        }
        if (normalized == "monthly")
        {
            return await _salesOrderRepository.GetSalesTrendAsync(new DateTime(now.Year, now.Month, 1).AddMonths(-11), tomorrow, TrendGranularity.Month, companyId, businessUnitId);
        }
        if (normalized == "yearly")
        {
            return await _salesOrderRepository.GetSalesTrendAsync(new DateTime(now.Year, 1, 1).AddYears(-4), tomorrow, TrendGranularity.Year, companyId, businessUnitId);
        }

        return await _salesOrderRepository.GetSalesTrendAsync(now.Date.AddDays(-13), tomorrow, TrendGranularity.Day, companyId, businessUnitId);
    }

    private static (DateTime From, DateTime ToExclusive) ResolveRange(string range, DateTime todayStart, DateTime tomorrow)
    {
        if (range == "7day")
        {
            return (todayStart.AddDays(-6), tomorrow);
        }
        if (range == "1month")
        {
            return (todayStart.AddMonths(-1).AddDays(1), tomorrow);
        }
        if (range == "1year")
        {
            return (todayStart.AddYears(-1).AddDays(1), tomorrow);
        }

        return (todayStart, tomorrow);
    }
}
