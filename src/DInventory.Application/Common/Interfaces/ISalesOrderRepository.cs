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
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns sales across every company.</summary>
    Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>All completed-or-not sales for one customer, newest first - feeds the Customer
    /// Details "Sales History" panel, mirrors IPurchaseRepository.GetForSupplierAsync.</summary>
    Task<IEnumerable<SalesOrder>> GetForCustomerAsync(int customerId);
    Task<int> CreateAsync(SalesOrder order);
    Task<string> GenerateNextInvoiceNoAsync();
    /// <summary>All the dashboard-facing aggregate reads below accept an optional companyId /
    /// businessUnitId scope: companyId = 0 (the default) bypasses company filtering entirely
    /// (superuser "see everything" mode), and a null businessUnitId bypasses BU filtering (used
    /// when the acting user isn't tied to one specific business unit).</summary>
    Task<decimal> GetSalesTotalAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);
    Task<int> GetOrderCountAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);
    Task<IEnumerable<SalesSummaryPoint>> GetSalesTrendAsync(DateTime fromDate, DateTime toDateExclusive, TrendGranularity granularity, int companyId = 0, int? businessUnitId = null);
    Task<IEnumerable<SalesReportRow>> GetReportRowsAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0);
    Task<IEnumerable<TopProduct>> GetTopProductsAsync(DateTime fromDate, DateTime toDateExclusive, int take = 5, int companyId = 0, int? businessUnitId = null);
    Task<bool> CancelAsync(int salesOrderId);

    /// <summary>Revenue minus cost-of-goods-sold across completed sales in the period (joins
    /// SalesOrderItems -> ProductVariants -> Products -> Price for CostPrice).</summary>
    Task<decimal> GetProfitTotalAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);

    /// <summary>Sum of NetAmount for completed sales paid by the given PaymentMethod (e.g. "CASH").</summary>
    Task<decimal> GetSalesTotalByMethodAsync(DateTime fromDate, DateTime toDateExclusive, string paymentMethod, int companyId = 0, int? businessUnitId = null);

    /// <summary>Total units sold (sum of SalesOrderItems.Quantity) across completed sales in the
    /// period - feeds the Dashboard's "Total Product Sold" stat.</summary>
    Task<int> GetTotalQuantitySoldAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null);

    Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0);
    Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0);

    /// <summary>Per-customer order count + total spend across all their completed orders (all-time -
    /// feeds the Customer Report's loyalty-points-at-a-glance view).</summary>
    Task<IEnumerable<CustomerReportRow>> GetCustomerSummaryReportAsync(int companyId = 0);
}
