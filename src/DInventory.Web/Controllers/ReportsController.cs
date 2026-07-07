using System.Text;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Reports;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("REPORTS")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly ICompanyContextService _companyContextService;

    public ReportsController(IReportService reportService, ICompanyContextService companyContextService)
    {
        _reportService = reportService;
        _companyContextService = companyContextService;
    }

    /// <summary>Reports hub - the sidebar's "Reports" menu item links here (a single top-level menu
    /// entry), with tiles/links out to each individual report.</summary>
    public IActionResult Index() => View();

    public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetSalesReportAsync(from, to, effectiveCompanyId);
        return View(rows);
    }

    public async Task<IActionResult> SalesExport(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetSalesReportAsync(from, to, effectiveCompanyId);

        var csv = new StringBuilder();
        csv.AppendLine("Date,Invoice No,Customer,Sub Total,Discount,Tax,Net Amount,Status,Created By");
        foreach (var row in rows)
        {
            csv.AppendLine($"{row.SaleDate:yyyy-MM-dd HH:mm},{row.InvoiceNo},{row.CustomerName},{row.SubTotal},{row.DiscountAmount},{row.TaxAmount},{row.NetAmount},{row.Status},{row.CreatedByName}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"sales-report-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Stock()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetStockReportAsync(effectiveCompanyId);
        return View(rows);
    }

    public async Task<IActionResult> StockExport()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetStockReportAsync(effectiveCompanyId);

        var csv = new StringBuilder();
        csv.AppendLine("Product Code,Product Name,Category,Quantity On Hand,Reorder Level,Cost Price,Selling Price,Stock Value,Low Stock");
        foreach (var row in rows)
        {
            csv.AppendLine($"{row.ProductCode},{row.ProductName},{row.CategoryName},{row.QuantityOnHand},{row.ReorderLevel},{row.CostPrice},{row.SellingPrice},{row.StockValue},{row.IsLowStock}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"stock-report-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Profit(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetProductProfitReportAsync(from, to.AddDays(1), effectiveCompanyId);
        return View(rows);
    }

    public async Task<IActionResult> CategorySales(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetCategorySalesReportAsync(from, to.AddDays(1), effectiveCompanyId);
        return View(rows);
    }

    /// <summary>All-time by design (no date range) - the customer report is a lifetime relationship
    /// view (loyalty points, total spend since day one), not a period snapshot.</summary>
    public async Task<IActionResult> Customers()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetCustomerReportAsync(effectiveCompanyId);
        return View(rows);
    }

    public async Task<IActionResult> Expenses(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetExpenseReportAsync(from, to.AddDays(1), effectiveCompanyId);
        var summary = await _reportService.GetExpenseSummaryAsync(from, to.AddDays(1), effectiveCompanyId);

        ViewBag.Summary = summary;
        return View(rows);
    }

    /// <summary>GrossProfit = SalesAmount - PurchaseAmount - ExpenseAmount, bucketed by calendar
    /// month. Defaults to the last 30 days like every other date-ranged report on this page.</summary>
    public async Task<IActionResult> GrossProfit(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-29);
        var to = toDate ?? DateTime.UtcNow.Date;

        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var rows = await _reportService.GetGrossProfitReportAsync(from, to, effectiveCompanyId);
        return View(rows);
    }
}
