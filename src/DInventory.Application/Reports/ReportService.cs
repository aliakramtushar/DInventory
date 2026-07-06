using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Reports;

public class ReportService : IReportService
{
    // Dead stock = no sale recorded in this many days (and it has been in the system at least that
    // long implicitly, since LastSoldAt would be null for a brand-new item too - acceptable for a
    // simple Phase 1 flag rather than a full slow-moving-inventory analysis).
    private const int DeadStockDays = 60;

    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly IExpenseRepository _expenseRepository;

    public ReportService(
        ISalesOrderRepository salesOrderRepository,
        IStockRepository stockRepository,
        IPriceRepository priceRepository,
        IProductVariantRepository variantRepository,
        IExpenseRepository expenseRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _stockRepository = stockRepository;
        _priceRepository = priceRepository;
        _variantRepository = variantRepository;
        _expenseRepository = expenseRepository;
    }

    public Task<IEnumerable<SalesReportRow>> GetSalesReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0)
        => _salesOrderRepository.GetReportRowsAsync(fromDate.Date, toDate.Date.AddDays(1), companyId);

    public async Task<IEnumerable<StockReportRow>> GetStockReportAsync(int companyId = 0)
    {
        // Reports are company-scoped via the caller's effective companyId (0 = SuperAdmin "All
        // Companies" view - a valid aggregate read for report pages, never for creates).
        var stockRows = (await _stockRepository.GetAllAsync(companyId)).ToList();
        var result = new List<StockReportRow>();
        var deadStockCutoff = DateTime.UtcNow.AddDays(-DeadStockDays);

        foreach (var stock in stockRows)
        {
            var variant = await _variantRepository.GetByIdAsync(stock.ProductVariantId);
            var price = variant is null ? null : await _priceRepository.GetActivePriceAsync(variant.ProductId);
            var reorderLevel = stock.ReorderLevel ?? variant?.ReorderLevel ?? 0;
            var lastSoldAt = await _stockRepository.GetLastSoldAtAsync(stock.ProductVariantId);

            result.Add(new StockReportRow
            {
                ProductCode = stock.ProductCode ?? variant?.ProductCode ?? string.Empty,
                ProductName = BuildDisplayName(stock.ProductName ?? variant?.ProductName, stock.SizeName ?? variant?.SizeName),
                CategoryName = variant?.CategoryName,
                QuantityOnHand = stock.QuantityOnHand,
                ReorderLevel = reorderLevel,
                CostPrice = price?.CostPrice,
                SellingPrice = price?.SellingPrice,
                StockValue = (price?.CostPrice ?? 0) * stock.QuantityOnHand,
                IsLowStock = stock.QuantityOnHand <= reorderLevel,
                LastSoldAt = lastSoldAt,
                IsDeadStock = lastSoldAt is null || lastSoldAt < deadStockCutoff
            });
        }

        return result;
    }

    public Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0)
        => _salesOrderRepository.GetProductProfitReportAsync(fromDate.Date, toDate.Date.AddDays(1), companyId);

    public Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0)
        => _salesOrderRepository.GetCategorySalesReportAsync(fromDate.Date, toDate.Date.AddDays(1), companyId);

    public Task<IEnumerable<CustomerReportRow>> GetCustomerReportAsync(int companyId = 0)
        => _salesOrderRepository.GetCustomerSummaryReportAsync(companyId);

    public async Task<IEnumerable<Expense>> GetExpenseReportAsync(DateTime fromDate, DateTime toDate, int companyId = 0)
    {
        // Reuse the paged repository method with a page size large enough to cover a normal report
        // range in one page - this stays a reporting/export view, not a paginated list of its own.
        var result = await _expenseRepository.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 5000 }, fromDate.Date, toDate.Date.AddDays(1), category: null, companyId: companyId);
        return result.Items;
    }

    public Task<IEnumerable<ExpenseCategoryTotal>> GetExpenseSummaryAsync(DateTime fromDate, DateTime toDate, int companyId = 0)
        => _expenseRepository.GetSummaryByCategoryAsync(fromDate.Date, toDate.Date.AddDays(1), companyId);

    private static string BuildDisplayName(string? productName, string? sizeName)
    {
        productName ??= string.Empty;
        return string.IsNullOrWhiteSpace(sizeName) ? productName : $"{productName} ({sizeName})";
    }
}
