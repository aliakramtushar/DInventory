using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Customers;
using DInventory.Application.Sales;
using DInventory.Application.Tenancy;
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
    private readonly ILoyaltyService _loyaltyService;
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICustomerService _customerService;

    public SalesController(
        ISalesService salesService,
        IProductVariantService productVariantService,
        ILoyaltyService loyaltyService,
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService,
        ICustomerService customerService)
    {
        _salesService = salesService;
        _productVariantService = productVariantService;
        _loyaltyService = loyaltyService;
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
        _customerService = customerService;
    }

    public async Task<IActionResult> Index(int? businessUnitId, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _salesService.GetPagedAsync(request, effectiveCompanyId, businessUnitId, fromDate, toDate?.AddDays(1));

        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<DInventory.Domain.Entities.BusinessUnit>();

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var order = await _salesService.GetByIdAsync(id);
        if (order is null)
        {
            return NotFound();
        }

        if (!currentUser.IsSuperCompany && order.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(order);
    }

    [HttpGet]
    [PermissionAuthorize("SALES", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
        ViewBag.EffectiveCompanyId = effectiveCompanyId;

        await PopulateFormDataAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SALES", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? customerId, string? newCustomerName, string? newCustomerMobile, string? discountType, decimal discountValue,
        decimal taxAmount, string paymentStatus, string? paymentMethod, string? remarks, int redeemPoints, string itemsJson)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        if (effectiveCompanyId <= 0)
        {
            TempData["ErrorMessage"] = "Please select a company from the top navigation bar before recording a sale.";
            ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
            ViewBag.EffectiveCompanyId = effectiveCompanyId;
            await PopulateFormDataAsync();
            return View();
        }

        var request = new CreateSaleRequest
        {
            CustomerId = customerId,
            NewCustomerName = newCustomerName,
            NewCustomerMobile = newCustomerMobile,
            DiscountType = string.Equals(discountType, "FIXED", StringComparison.OrdinalIgnoreCase) ? "FIXED" : "PERCENT",
            DiscountValue = discountValue,
            TaxAmount = taxAmount,
            PaymentStatus = string.IsNullOrWhiteSpace(paymentStatus) ? "PAID" : paymentStatus,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "CASH" : paymentMethod,
            Remarks = remarks,
            RedeemPoints = redeemPoints
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

        var effectiveBusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();
        var result = await _salesService.CreateSaleAsync(request, currentUser.UserId, effectiveCompanyId, effectiveBusinessUnitId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
            ViewBag.EffectiveCompanyId = effectiveCompanyId;
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
            barcode = variant.Barcode,
            price = variant.SellingPrice ?? 0,
            stock = variant.QuantityOnHand ?? 0
        });
    }

    /// <summary>AJAX lookup for the "Redeem Points" box on Sales/Create - returns the selected
    /// customer's current loyalty balance and the shop's current redeem rate, so the screen can
    /// show/validate a redemption before the sale is actually submitted.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomerLoyalty(int customerId)
    {
        var customer = await _customerService.GetByIdAsync(customerId);
        if (customer is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && customer.CompanyId != currentUser.CompanyId)
        {
            return NotFound();
        }

        var settings = await _loyaltyService.GetSettingsAsync();
        var balance = await _loyaltyService.GetBalanceAsync(customerId);

        return Json(new
        {
            balance,
            isEnabled = settings.IsEnabled && settings.PointValueOnRedeem > 0,
            pointValueOnRedeem = settings.PointValueOnRedeem
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

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && variant.CompanyId != currentUser.CompanyId)
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
        // Manual fallback dropdown: every active variant belonging to whichever company is currently
        // selected in the navbar - a generous page size since this is a simple <select> fallback for
        // when there's no scanner handy (not itself paginated UI).
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var variantsPage = await _productVariantService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 500 }, effectiveCompanyId, onlyActive: true);
        ViewBag.Variants = variantsPage.Items;
        ViewBag.Customers = await _salesService.GetCustomersAsync();
    }
}
