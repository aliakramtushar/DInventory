using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public enum TrendGranularity
{
    Day,
    Week,
    Month,
    Year
}

public interface ISalesOrderRepository
{
    Task<SalesOrder?> GetByIdAsync(int salesOrderId);
    Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>All completed-or-not sales for one customer, newest first - feeds the Customer
    /// Details "Sales History" panel, mirrors IPurchaseRepository.GetForSupplierAsync.</summary>
    Task<IEnumerable<SalesOrder>> GetForCustomerAsync(int customerId);
    Task<int> CreateAsync(SalesOrder order);
    Task<string> GenerateNextInvoiceNoAsync();
    Task<decimal> GetSalesTotalAsync(DateTime fromDate, DateTime toDateExclusive);
    Task<int> GetOrderCountAsync(DateTime fromDate, DateTime toDateExclusive);
    Task<IEnumerable<SalesSummaryPoint>> GetSalesTrendAsync(DateTime fromDate, DateTime toDateExclusive, TrendGranularity granularity);
    Task<IEnumerable<SalesReportRow>> GetReportRowsAsync(DateTime fromDate, DateTime toDateExclusive);
    Task<IEnumerable<TopProduct>> GetTopProductsAsync(DateTime fromDate, DateTime toDateExclusive, int take = 5);
    Task<bool> CancelAsync(int salesOrderId);

    /// <summary>Revenue minus cost-of-goods-sold across completed sales in the period (joins
    /// SalesOrderItems -> ProductVariants -> Products -> Price for CostPrice).</summary>
    Task<decimal> GetProfitTotalAsync(DateTime fromDate, DateTime toDateExclusive);

    /// <summary>Sum of NetAmount for completed sales paid by the given PaymentMethod (e.g. "CASH").</summary>
    Task<decimal> GetSalesTotalByMethodAsync(DateTime fromDate, DateTime toDateExclusive, string paymentMethod);

    Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDateExclusive);
    Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDateExclusive);

    /// <summary>Per-customer order count + total spend across all their completed orders (all-time -
    /// feeds the Customer Report's loyalty-points-at-a-glance view).</summary>
    Task<IEnumerable<CustomerReportRow>> GetCustomerSummaryReportAsync();
}
