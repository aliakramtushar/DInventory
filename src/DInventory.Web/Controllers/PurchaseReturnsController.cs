using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Purchasing;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("PURCHASE_RETURNS")]
public class PurchaseReturnsController : Controller
{
    private readonly IPurchaseReturnService _purchaseReturnService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public PurchaseReturnsController(
        IPurchaseReturnService purchaseReturnService,
        IPurchaseService purchaseService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _purchaseReturnService = purchaseReturnService;
        _purchaseService = purchaseService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20 };
        var result = await _purchaseReturnService.GetPagedAsync(request);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var purchaseReturn = await _purchaseReturnService.GetByIdAsync(id);
        if (purchaseReturn is null)
        {
            return NotFound();
        }

        return View(purchaseReturn);
    }

    /// <summary>Purchase returns are always linked to an original purchase invoice - same
    /// lookup-then-line-picker flow as SalesReturnsController.Create.</summary>
    [HttpGet]
    [PermissionAuthorize("PURCHASE_RETURNS", PermissionAction.Create)]
    public async Task<IActionResult> Create(int? purchaseId, string? invoiceNo)
    {
        if (purchaseId is null && !string.IsNullOrWhiteSpace(invoiceNo))
        {
            var matches = await _purchaseService.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 10, Search = invoiceNo });
            var exact = matches.Items.FirstOrDefault(p => string.Equals(p.PurchaseInvoiceNo, invoiceNo, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return RedirectToAction(nameof(Create), new { purchaseId = exact.PurchaseId });
            }

            ViewBag.SearchResults = matches.Items;
            ViewBag.SearchedInvoiceNo = invoiceNo;
            return View();
        }

        if (purchaseId is null)
        {
            return View();
        }

        var purchase = await _purchaseService.GetByIdAsync(purchaseId.Value);
        if (purchase is null)
        {
            TempData["ErrorMessage"] = "Purchase invoice not found.";
            return View();
        }

        return View(purchase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PURCHASE_RETURNS", PermissionAction.Create)]
    public async Task<IActionResult> Create(int purchaseId, string? reason, string? remarks, string itemsJson)
    {
        var request = new CreatePurchaseReturnRequest
        {
            PurchaseId = purchaseId,
            Reason = reason,
            Remarks = remarks
        };

        try
        {
            request.Items = System.Text.Json.JsonSerializer.Deserialize<List<CreatePurchaseReturnItemInput>>(itemsJson ?? "[]",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CreatePurchaseReturnItemInput>();
        }
        catch (System.Text.Json.JsonException)
        {
            request.Items = new List<CreatePurchaseReturnItemInput>();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _purchaseReturnService.CreateReturnAsync(request, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Create), new { purchaseId });
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "PurchaseReturns", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Return recorded and stock adjusted.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }
}
