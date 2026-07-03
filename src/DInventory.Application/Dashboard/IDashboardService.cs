using DInventory.Application.Common.Models;

namespace DInventory.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync();
    Task<IEnumerable<SalesSummaryPoint>> GetTrendAsync(string range);
}
