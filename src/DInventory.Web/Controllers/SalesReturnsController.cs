using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Sales;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("SALES_RETURNS")]
public class SalesReturnsController : Controller
{
    private readonly ISalesReturnService _salesReturnService;
    private readonly ISalesService _salesService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IAuditLogService _auditLogService;

    public SalesReturnsController(
        ISalesReturnService salesReturnService,
        ISalesService salesService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IAuditLogService auditLogService)
    {
        _salesReturnService = salesReturnService;
        _salesService = salesService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _salesReturnService.GetPagedAsync(request, effectiveCompanyId);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var salesReturn = await _salesReturnService.GetByIdAsync(id);
        if (salesReturn is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && salesReturn.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(salesReturn);
    }

    /// <summary>Sale returns are always linked to an original invoice. With no invoice resolved
    /// yet this shows a lookup box; with ?salesOrderId= (or a resolved ?invoiceNo=) it shows the
    /// original sale's lines with a "Return Qty" input per line, capped at what's still
    /// returnable (sold qty minus already-returned qty across any prior returns).</summary>
    [HttpGet]
    [PermissionAuthorize("SALES_RETURNS", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? salesOrderId, string? invoiceNo)
    {
        if (salesOrderId is null && !string.IsNullOrWhiteSpace(invoiceNo))
        {
            var matches = await _salesService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 10, Search = invoiceNo }, _companyContextService.GetEffectiveCompanyId());
            var exact = matches.Items.FirstOrDefault(o => string.Equals(o.InvoiceNo, invoiceNo, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return RedirectToAction(nameof(Create), new { salesOrderId = exact.SalesOrderId });
            }

            ViewBag.SearchResults = matches.Items;
            ViewBag.SearchedInvoiceNo = invoiceNo;
            return View();
        }

        if (salesOrderId is null)
        {
            return View();
        }

        var order = await _salesService.GetByIdAsync(salesOrderId.Value);
        if (order is null)
        {
            TempData["ErrorMessage"] = "Invoice not found.";
            return View();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && order.CompanyId != currentUser.CompanyId)
        {
            TempData["ErrorMessage"] = "Invoice not found.";
            return View();
        }

        if (order.Status == "CANCELLED")
        {
            TempData["ErrorMessage"] = "This sale was cancelled - its stock has already been restored, so it can't be returned.";
            return View();
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SALES_RETURNS", PermissionAction.Create)]
    public async Task<IActionResult> Create(int salesOrderId, string? reason, string? remarks, string itemsJson)
    {
        var request = new CreateSalesReturnRequest
        {
            SalesOrderId = salesOrderId,
            Reason = reason,
            Remarks = remarks
        };

        try
        {
            request.Items = System.Text.Json.JsonSerializer.Deserialize<List<CreateSalesReturnItemInput>>(itemsJson ?? "[]",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreateSalesReturnItemInput>();
        }
        catch (System.Text.Json.JsonException)
        {
            request.Items = new List<CreateSalesReturnItemInput>();
        }

        var currentUser = _currentUserService.GetCurrentUser();

        var order = await _salesService.GetByIdAsync(salesOrderId);
        if (order is null || (!currentUser.IsSuperCompany && order.CompanyId != currentUser.CompanyId))
        {
            TempData["ErrorMessage"] = "Invoice not found.";
            return RedirectToAction(nameof(Create));
        }

        var result = await _salesReturnService.CreateReturnAsync(request, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Create), new { salesOrderId });
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "SalesReturns", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Return recorded and stock restored.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }
}
