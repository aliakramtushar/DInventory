using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Sales;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("SALES")]
public class SalesController : Controller
{
    private readonly ISalesService _salesService;
    private readonly IProductVariantService _productVariantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public SalesController(
        ISalesService salesService,
        IProductVariantService productVariantService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _salesService = salesService;
        _productVariantService = productVariantService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _salesService.GetPagedAsync(request, fromDate, toDate?.AddDays(1));

        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _salesService.GetByIdAsync(id);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet]
    [PermissionAuthorize("SALES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateFormDataAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SALES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? customerId, string? newCustomerName, decimal discountAmount,
        decimal taxAmount, string paymentStatus, string? paymentMethod, string? remarks, string itemsJson)
    {
        var request = new CreateSaleRequest
        {
            CustomerId = customerId,
            NewCustomerName = newCustomerName,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            PaymentStatus = string.IsNullOrWhiteSpace(paymentStatus) ? "PAID" : paymentStatus,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "CASH" : paymentMethod,
            Remarks = remarks
        };

        try
        {
            request.Items = System.Text.Json.JsonSerializer.Deserialize<List<CreateSaleItem>>(itemsJson ?? "[]",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreateSaleItem>();
        }
        catch (System.Text.Json.JsonException)
        {
            request.Items = new List<CreateSaleItem>();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _salesService.CreateSaleAsync(request, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            await PopulateFormDataAsync();
            return View();
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "SalesOrders", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Sale recorded successfully.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SALES", PermissionAction.Delete)]
    public async Task<IActionResult> Cancel(int id)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _salesService.CancelSaleAsync(id, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CANCEL", "SalesOrders", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Sale cancelled and stock returned.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Scan-to-cart AJAX endpoint for the POS screen: resolves a barcode into a sellable line
    /// (name, size, price, stock) via ISalesService.ScanForSaleAsync so it can be added to the cart
    /// with quantity 1 without a page reload.</summary>
    [HttpGet]
    public async Task<IActionResult> ScanForSale(string barcode)
    {
        var result = await _salesService.ScanForSaleAsync(barcode);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        var variant = result.Data!;
        return Json(new
        {
            productVariantId = variant.ProductVariantId,
            productName = variant.ProductName,
            sizeName = variant.SizeName,
            barcode = variant.Barcode,
            price = variant.SellingPrice ?? 0,
            stock = variant.QuantityOnHand ?? 0
        });
    }

    /// <summary>Manual-dropdown fallback lookup (no scanner handy) - returns price/stock for a
    /// specific product variant id chosen from the picker.</summary>
    [HttpGet]
    public async Task<IActionResult> GetVariantPrice(int productVariantId)
    {
        var variant = await _productVariantService.GetByIdAsync(productVariantId);
        if (variant is null)
        {
            return NotFound();
        }

        return Json(new
        {
            price = variant.SellingPrice ?? 0,
            stock = variant.QuantityOnHand ?? 0,
            productName = variant.ProductName,
            sizeName = variant.SizeName,
            barcode = variant.Barcode
        });
    }

    private async Task PopulateFormDataAsync()
    {
        // Manual fallback dropdown: every active variant, a generous page size since this is a
        // simple <select> fallback for when there's no scanner handy (not itself paginated UI).
        var variantsPage = await _productVariantService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 500 }, onlyActive: true);
        ViewBag.Variants = variantsPage.Items;
        ViewBag.Customers = await _salesService.GetCustomersAsync();
    }
}
