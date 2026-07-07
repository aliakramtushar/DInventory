using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Purchasing;
using DInventory.Application.Tenancy;
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
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public PurchasesController(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IProductVariantService productVariantService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _productVariantService = productVariantService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int? businessUnitId, int? supplierId, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _purchaseService.GetPagedAsync(request, effectiveCompanyId, businessUnitId, supplierId);

        ViewData["SupplierId"] = supplierId;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewBag.Suppliers = await _supplierService.GetAllAsync(effectiveCompanyId, onlyActive: true);
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<DInventory.Domain.Entities.BusinessUnit>();

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var purchase = await _purchaseService.GetByIdAsync(id);
        if (purchase is null)
        {
            return NotFound();
        }

        if (!currentUser.IsSuperCompany && purchase.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(purchase);
    }

    [HttpGet]
    [PermissionAuthorize("PURCHASES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? supplierId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
        ViewBag.EffectiveCompanyId = effectiveCompanyId;

        await PopulateFormDataAsync();
        ViewData["SupplierId"] = supplierId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PURCHASES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int supplierId, decimal paidAmount, decimal discount, string? remarks, string itemsJson)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        if (effectiveCompanyId <= 0)
        {
            TempData["ErrorMessage"] = "Please select a company from the top navigation bar before receiving a purchase.";
            ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
            ViewBag.EffectiveCompanyId = effectiveCompanyId;
            await PopulateFormDataAsync();
            ViewData["SupplierId"] = supplierId;
            return View();
        }

        var request = new CreatePurchaseRequest
        {
            SupplierId = supplierId,
            PaidAmount = paidAmount,
            Discount = discount,
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
            ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
            ViewBag.EffectiveCompanyId = effectiveCompanyId;
            await PopulateFormDataAsync();
            ViewData["SupplierId"] = supplierId;
            return View();
        }

        var effectiveBusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();
        var result = await _purchaseService.CreateAsync(request, currentUser.UserId, effectiveCompanyId, effectiveBusinessUnitId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
            ViewBag.EffectiveCompanyId = effectiveCompanyId;
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

        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && variant.CompanyId != currentUser.CompanyId)
        {
            return BadRequest(new { message = $"No product found for barcode '{barcode}'." });
        }

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
        // Manual fallback dropdown - scoped to whichever company is currently selected in the navbar
        // so a purchase can only ever be receipted against that company's own products/suppliers.
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var variantsPage = await _productVariantService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 500 }, effectiveCompanyId, onlyActive: true);
        ViewBag.Variants = variantsPage.Items;
        ViewBag.Suppliers = await _supplierService.GetAllAsync(effectiveCompanyId, onlyActive: true);
    }
}
