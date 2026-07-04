using DInventory.Application.Audit;
using DInventory.Application.Barcoding;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
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
/// </summary>
[Authorize]
[PermissionAuthorize("BARCODEGEN")]
public class BarcodeGeneratorController : Controller
{
    private readonly IGeneratedBarcodeLabelService _labelService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public BarcodeGeneratorController(
        IGeneratedBarcodeLabelService labelService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _labelService = labelService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    /// <summary>The interactive generator page: type a product name/brand/size/price, generate as
    /// many labels as needed in this session, then print them all at once.</summary>
    public IActionResult Index() => View();

    /// <summary>Paged history of every label ever generated (for reference/reuse lookups).</summary>
    public async Task<IActionResult> History(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _labelService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    /// <summary>AJAX endpoint backing the generator page - persists the label (generating a barcode
    /// automatically when none is supplied) and returns the barcode string for JsBarcode to render.</summary>
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
        var result = await _labelService.GenerateAsync(
            request.ManualBarcode,
            request.ProductName,
            request.BrandName,
            request.SizeName,
            request.Price,
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
            price = result.Data.Price
        });
    }

    public class GenerateLabelRequest
    {
        public string? ManualBarcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? SizeName { get; set; }
        public decimal? Price { get; set; }
    }
}
