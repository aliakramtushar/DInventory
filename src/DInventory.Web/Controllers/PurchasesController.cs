using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Purchasing;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("PURCHASES")]
public class PurchasesController : Controller
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IProductVariantService _productVariantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public PurchasesController(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IProductVariantService productVariantService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _productVariantService = productVariantService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int? supplierId, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _purchaseService.GetPagedAsync(request, supplierId);

        ViewData["SupplierId"] = supplierId;
        ViewBag.Suppliers = await _supplierService.GetAllAsync(onlyActive: true);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var purchase = await _purchaseService.GetByIdAsync(id);
        if (purchase is null)
        {
            return NotFound();
        }

        return View(purchase);
    }

    [HttpGet]
    [PermissionAuthorize("PURCHASES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? supplierId)
    {
        await PopulateFormDataAsync();
        ViewData["SupplierId"] = supplierId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PURCHASES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int supplierId, decimal paidAmount, string? remarks, string itemsJson)
    {
        var request = new CreatePurchaseRequest
        {
            SupplierId = supplierId,
            PaidAmount = paidAmount,
            Remarks = remarks
        };

        try
        {
            request.Items = System.Text.Json.JsonSerializer.Deserialize<List<CreatePurchaseItemInput>>(itemsJson ?? "[]",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreatePurchaseItemInput>();
        }
        catch (System.Text.Json.JsonException)
        {
            request.Items = new List<CreatePurchaseItemInput>();
        }

        if (request.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "Add at least one item to receive.";
            await PopulateFormDataAsync();
            ViewData["SupplierId"] = supplierId;
            return View();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _purchaseService.CreateAsync(request, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            await PopulateFormDataAsync();
            ViewData["SupplierId"] = supplierId;
            return View();
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Purchases", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Purchase received and stock updated.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    /// <summary>Barcode-scan lookup for the receiving screen (mirrors Sales' ScanForSale) - resolves
    /// an existing variant's barcode so the receiving clerk can add a line without hunting through a
    /// dropdown.</summary>
    [HttpGet]
    public async Task<IActionResult> ScanForPurchase(string barcode)
    {
        var result = await _productVariantService.LookupByBarcodeAsync(barcode);
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
            colorName = variant.ColorName,
            barcode = variant.Barcode,
            costPrice = variant.CostPrice ?? 0
        });
    }

    private async Task PopulateFormDataAsync()
    {
        var variantsPage = await _productVariantService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 500 }, onlyActive: true);
        ViewBag.Variants = variantsPage.Items;
        ViewBag.Suppliers = await _supplierService.GetAllAsync(onlyActive: true);
    }
}
