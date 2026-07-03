using DInventory.Application.Common.Models;

namespace DInventory.Application.Reports;

public interface IReportService
{
    Task<IEnumerable<SalesReportRow>> GetSalesReportAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<StockReportRow>> GetStockReportAsync();
    Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<CustomerReportRow>> GetCustomerReportAsync();
    Task<IEnumerable<Domain.Entities.Expense>> GetExpenseReportAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<ExpenseCategoryTotal>> GetExpenseSummaryAsync(DateTime fromDate, DateTime toDate);
}
