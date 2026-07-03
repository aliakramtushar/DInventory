using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Inventory;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("STOCK")]
public class StockController : Controller
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public StockController(IStockService stockService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(bool onlyLowStock = false, string? search = null, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _stockService.GetPagedAsync(request, onlyLowStock);

        ViewData["OnlyLowStock"] = onlyLowStock;
        ViewData["Search"] = search;
        return View(result);
    }

    public async Task<IActionResult> History(int productVariantId)
    {
        var transactions = await _stockService.GetTransactionsAsync(productVariantId);
        var stock = await _stockService.GetByVariantIdAsync(productVariantId);
        ViewBag.Stock = stock;
        return View(transactions);
    }

    /// <summary>Adjust stock for a variant selected from the list (variant id known already).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("STOCK", PermissionAction.Edit)]
    public async Task<IActionResult> Adjust(int productVariantId, int quantity, string transactionType, string? remarks)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _stockService.AdjustStockAsync(productVariantId, quantity, transactionType, remarks, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "STOCK_ADJUST", "Stock", productVariantId.ToString(),
                newValues: $"{transactionType} {quantity}", ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Stock adjusted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Barcode-first stock entry: scan (or type) a barcode to find the variant and adjust its
    /// stock in one step, without needing to look it up in the list first.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("STOCK", PermissionAction.Edit)]
    public async Task<IActionResult> AdjustByBarcode(string barcode, int quantity, string transactionType, string? remarks)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _stockService.AdjustStockByBarcodeAsync(barcode, quantity, transactionType, remarks, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "STOCK_ADJUST", "Stock", barcode,
                newValues: $"{transactionType} {quantity}", ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = $"Stock adjusted successfully for barcode '{barcode}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>AJAX lookup used by the barcode-scan stock entry form to show the matched product/size
    /// before the adjustment is submitted.</summary>
    [HttpGet]
    public async Task<IActionResult> LookupByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(new { message = "Please scan or enter a barcode." });
        }

        // Reuse the same barcode resolution the adjustment itself will use, but with quantity 0 /
        // no-op semantics isn't available here, so we look the variant up through the stock row
        // returned once we know the barcode resolves - simplest is to just try a 0 quantity guard.
        var stock = await _stockService.GetAllAsync(search: barcode);
        var match = stock.FirstOrDefault(s => string.Equals(s.Barcode, barcode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return NotFound(new { message = $"No product found for barcode '{barcode}'." });
        }

        return Json(new
        {
            productVariantId = match.ProductVariantId,
            productName = match.ProductName,
            sizeName = match.SizeName,
            barcode = match.Barcode,
            quantityOnHand = match.QuantityOnHand,
            reorderLevel = match.ReorderLevel
        });
    }
}
