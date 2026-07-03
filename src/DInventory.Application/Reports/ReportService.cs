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

    public Task<IEnumerable<SalesReportRow>> GetSalesReportAsync(DateTime fromDate, DateTime toDate)
        => _salesOrderRepository.GetReportRowsAsync(fromDate.Date, toDate.Date.AddDays(1));

    public async Task<IEnumerable<StockReportRow>> GetStockReportAsync()
    {
        var stockRows = (await _stockRepository.GetAllAsync()).ToList();
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

    public Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDate)
        => _salesOrderRepository.GetProductProfitReportAsync(fromDate.Date, toDate.Date.AddDays(1));

    public Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDate)
        => _salesOrderRepository.GetCategorySalesReportAsync(fromDate.Date, toDate.Date.AddDays(1));

    public Task<IEnumerable<CustomerReportRow>> GetCustomerReportAsync()
        => _salesOrderRepository.GetCustomerSummaryReportAsync();

    public async Task<IEnumerable<Expense>> GetExpenseReportAsync(DateTime fromDate, DateTime toDate)
    {
        // Reuse the paged repository method with a page size large enough to cover a normal report
        // range in one page - this stays a reporting/export view, not a paginated list of its own.
        var result = await _expenseRepository.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 5000 }, fromDate.Date, toDate.Date.AddDays(1));
        return result.Items;
    }

    public Task<IEnumerable<ExpenseCategoryTotal>> GetExpenseSummaryAsync(DateTime fromDate, DateTime toDate)
        => _expenseRepository.GetSummaryByCategoryAsync(fromDate.Date, toDate.Date.AddDays(1));

    private static string BuildDisplayName(string? productName, string? sizeName)
    {
        productName ??= string.Empty;
        return string.IsNullOrWhiteSpace(sizeName) ? productName : $"{productName} ({sizeName})";
    }
}
