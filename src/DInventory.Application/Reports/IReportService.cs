using DInventory.Application.Common.Models;

namespace DInventory.Application.Reports;

public interface IReportService
{
    Task<IEnumerable<SalesReportRow>> GetSalesReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0);
    Task<IEnumerable<StockReportRow>> GetStockReportAsync(int companyId = 0);
    Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0);
    Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0);
    Task<IEnumerable<CustomerReportRow>> GetCustomerReportAsync(int companyId = 0);
    Task<IEnumerable<Domain.Entities.Expense>> GetExpenseReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0);
    Task<IEnumerable<ExpenseCategoryTotal>> GetExpenseSummaryAsync(DateTime fromDate, DateTime toDate, int companyId = 0);
}
