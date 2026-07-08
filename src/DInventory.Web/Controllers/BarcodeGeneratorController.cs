using DInventory.Application.Audit;
using DInventory.Application.Barcoding;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Tenancy;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>
/// "Print now, physically tag it, enter it into inventory later" workflow. Generating a label here
/// only writes a GeneratedBarcodeLabels row for later reference/reuse - it does NOT create a real
/// Product/ProductVariant. That happens afterwards on the Products screen, where the barcode field
/// accepts one of these previously generated (but still unlinked) codes.
///
/// Which company a label belongs to comes from the global company selector in the top navbar
/// (ICompanyContextService) - never from anything on this page itself. Non-SuperAdmin users are
/// always pinned to their own company. SuperAdmin must pick one real company from the navbar before
/// generating (the navbar's default "All Companies" is a read-only aggregate view and can't own a
/// newly generated label).
/// </summary>
[Authorize]
[PermissionAuthorize("BARCODEGEN")]
public class BarcodeGeneratorController : Controller
{
    private readonly IGeneratedBarcodeLabelService _labelService;
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public BarcodeGeneratorController(
        IGeneratedBarcodeLabelService labelService,
        ICompanyService companyService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _labelService = labelService;
        _companyService = companyService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    /// <summary>The interactive generator page: type a product name/brand/size/price, generate as
    /// many labels as needed in this session, then print them all at once.</summary>
    public async Task<IActionResult> Index()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
        ViewBag.EffectiveCompanyId = effectiveCompanyId;

        if (!currentUser.IsSuperCompany)
        {
            ViewBag.CompanyName = currentUser.CompanyName;
        }
        else if (effectiveCompanyId > 0)
        {
            var company = await _companyService.GetByIdAsync(effectiveCompanyId);
            ViewBag.CompanyName = company?.CompanyName;
        }
        // else: SuperAdmin still has "All Companies" selected in the navbar - the view shows a
        // prompt to pick a real company there before the Generate button is usable.
        // Note: the company is only used for the optional "include company name on the label" text
        // and for scoping this label to a tenant - it's never encoded into the barcode itself, since
        // a company-code prefix would break auto-detection on standard POS/EAN-13 scanners.

        // Business unit is entirely optional metadata on a label (never part of the barcode text
        // itself) - offered as a plain dropdown so the user can tag a label with one if they want to.
        ViewBag.BusinessUnits = await _businessUnitContextService.GetSelectableBusinessUnitsAsync();

        return View();
    }

    /// <summary>Paged history of every label ever generated (for reference/reuse lookups). Scoped to
    /// whichever company is currently selected in the navbar (0 = every company, SuperAdmin only).</summary>
    public async Task<IActionResult> History(string? search, int page = 1)
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _labelService.GetPagedAsync(request, effectiveCompanyId);

        ViewData["Search"] = search;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewBag.IsSuperCompany = _companyContextService.IsSuperCompany;

        return View(result);
    }

    /// <summary>AJAX endpoint backing the generator page - persists the label (generating a barcode
    /// automatically when none is supplied) and returns the barcode string for JsBarcode to render.
    /// Company is always resolved server-side from ICompanyContextService - the client never sends
    /// one, so there's nothing for a tampered request to spoof.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("BARCODEGEN", PermissionAction.Create)]
    public async Task<IActionResult> Generate([FromBody] GenerateLabelRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProductName))
        {
            return BadRequest(new { message = "Product name is required." });
        } 

        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        if (effectiveCompanyId <= 0)
        {
            return BadRequest(new { message = "Please select a company from the top navigation bar before generating a label." });
        }

        try
        {
            string? companyName;
            int? effectiveBusinessUnitId;

            if (currentUser.IsSuperCompany)
            {
                var company = await _companyService.GetByIdAsync(effectiveCompanyId);
                if (company is null || !company.IsActive)
                {
                    return BadRequest(new { message = "Selected company was not found or is inactive." });
                }

                companyName = company.CompanyName;
            }
            else
            {
                companyName = currentUser.CompanyName;
            }

            // The user can explicitly pick a business unit on the form; fall back to whatever the
            // navbar's business-unit context resolves to when they didn't pick one.
            effectiveBusinessUnitId = request.BusinessUnitId ?? await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

            // Company name is optional and only stored/shown on the label if the user opted in via the
            // "Include company name on the label" checkbox. Note there's no company CODE segment
            // anymore - the barcode itself is a plain EAN-13 numeric code so it scans on any
            // standard POS/handheld scanner without extra configuration.
            var companyNameForLabel = request.IncludeCompanyName ? companyName : null;

            var result = await _labelService.GenerateAsync(
                effectiveCompanyId,
                effectiveBusinessUnitId,
                request.BusinessUnitName,
                request.ManualBarcode,
                request.ProductName,
                request.BrandName,
                request.SizeName,
                companyNameForLabel,
                request.PriceCode,
                request.Price,
                request.BarcodeWidth,
                request.BarcodeHeight,
                currentUser.UserId);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = result.Error ?? "Unable to generate barcode label." });
            }

            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "GeneratedBarcodeLabels",
                result.Data!.LabelId.ToString(), ipAddress: currentUser.IpAddress);

            return Json(new
            {
                labelId = result.Data.LabelId,
                barcode = result.Data.Barcode,
                productName = result.Data.ProductName,
                brandName = result.Data.BrandName,
                sizeName = result.Data.SizeName,
                companyName = result.Data.CompanyName,
                businessUnitName = result.Data.BusinessUnitName,
                priceCode = result.Data.PriceCode,
                price = result.Data.Price,
                barcodeWidth = result.Data.BarcodeWidth,
                barcodeHeight = result.Data.BarcodeHeight
            });
        }
        catch (Exception ex)
        {
            // TEMPORARY diagnostic: surface the real exception message instead of letting it become an
            // opaque 500/HTML page the client-side fetch can't parse (which is why the banner was just
            // showing the generic "Unable to generate barcode label." fallback with no real information).
            // Remove this catch once the underlying issue is confirmed fixed - a raw exception message
            // should not normally be shown to end users.
            var detail = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { message = $"Unable to generate barcode label: {detail}" });
        }
    }

    public class GenerateLabelRequest
    {
        public string? ManualBarcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? SizeName { get; set; }
        public string? CompanyName { get; set; }
        public decimal? Price { get; set; }

        /// <summary>EAN-13 barcode module width in px. Null falls back to the service default (2).</summary>
        public int? BarcodeWidth { get; set; }

        /// <summary>Barcode height in px. Null falls back to the service default (50).</summary>
        public int? BarcodeHeight { get; set; }

        /// <summary>Optional - the user can tag a label with a specific business unit from a dropdown
        /// on the form. Purely metadata: never appears inside the barcode text itself.</summary>
        public int? BusinessUnitId { get; set; }
        public string? BusinessUnitName { get; set; }

        /// <summary>Optional price code embedded as a 5-digit segment of the generated EAN-13 barcode.
        /// Left out entirely (not padded with a placeholder) when left blank - the barcode's
        /// item-number segment just uses all available digits instead. Ignored when a manual barcode
        /// is supplied.</summary>
        public string? PriceCode { get; set; }

        /// <summary>Whether to store/print the company name on the label. Optional - defaults to false
        /// (not shown) unless the user checks the "Include company name on the label" box. The barcode
        /// itself never carries a company code - only this optional display name.</summary>
        public bool IncludeCompanyName { get; set; }
    }
}
