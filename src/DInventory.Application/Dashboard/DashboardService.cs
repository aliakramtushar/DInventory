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

    public async Task<DashboardStats> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var yearStart = new DateTime(now.Year, 1, 1);
        var tomorrow = todayStart.AddDays(1);

        var todayCashSales = await _salesOrderRepository.GetSalesTotalByMethodAsync(todayStart, tomorrow, "CASH");
        var todayExpenses = await _expenseRepository.GetTotalAsync(todayStart, tomorrow);

        var stats = new DashboardStats
        {
            TodaySales = await _salesOrderRepository.GetSalesTotalAsync(todayStart, tomorrow),
            WeekSales = await _salesOrderRepository.GetSalesTotalAsync(weekStart, tomorrow),
            MonthSales = await _salesOrderRepository.GetSalesTotalAsync(monthStart, tomorrow),
            YearSales = await _salesOrderRepository.GetSalesTotalAsync(yearStart, tomorrow),
            TodayOrderCount = await _salesOrderRepository.GetOrderCountAsync(todayStart, tomorrow),
            TotalProducts = await _productRepository.GetTotalActiveCountAsync(),
            LowStockCount = await _stockRepository.GetLowStockCountAsync(),
            TotalCustomers = await _customerRepository.GetTotalCountAsync(),
            TodayProfit = await _salesOrderRepository.GetProfitTotalAsync(todayStart, tomorrow),
            CashInHand = todayCashSales - todayExpenses,
            Trend = (await _salesOrderRepository.GetSalesTrendAsync(todayStart.AddDays(-13), tomorrow, TrendGranularity.Day)).ToList(),
            TopProducts = (await _salesOrderRepository.GetTopProductsAsync(monthStart, tomorrow, 5)).ToList()
        };

        return stats;
    }

    public async Task<IEnumerable<SalesSummaryPoint>> GetTrendAsync(string range)
    {
        var now = DateTime.UtcNow;
        var tomorrow = now.Date.AddDays(1);

        return range.ToLowerInvariant() switch
        {
            "daily" => await _salesOrderRepository.GetSalesTrendAsync(now.Date.AddDays(-13), tomorrow, TrendGranularity.Day),
            "weekly" => await _salesOrderRepository.GetSalesTrendAsync(now.Date.AddDays(-7 * 11), tomorrow, TrendGranularity.Week),
            "monthly" => await _salesOrderRepository.GetSalesTrendAsync(new DateTime(now.Year, now.Month, 1).AddMonths(-11), tomorrow, TrendGranularity.Month),
            "yearly" => await _salesOrderRepository.GetSalesTrendAsync(new DateTime(now.Year, 1, 1).AddYears(-4), tomorrow, TrendGranularity.Year),
            _ => await _salesOrderRepository.GetSalesTrendAsync(now.Date.AddDays(-13), tomorrow, TrendGranularity.Day)
        };
    }
}
