using DInventory.Application.Common.Models;

namespace DInventory.Application.Dashboard;

public interface IDashboardService
{
    /// <summary>companyId = 0 bypasses company filtering (superuser "see everything" mode); a null
    /// businessUnitId bypasses BU filtering. range is one of "today", "7day", "1month", "1year" and
    /// controls the RangeSales/TotalOrders/TotalProductSold/RangeProfit stats only.</summary>
    Task<DashboardStats> GetStatsAsync(int companyId = 0, int? businessUnitId = null, string range = "today");
    Task<IEnumerable<SalesSummaryPoint>> GetTrendAsync(string range, int companyId = 0, int? businessUnitId = null);
}
