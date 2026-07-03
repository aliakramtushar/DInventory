using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Purchasing;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("SUPPLIERS")]
public class SuppliersController : Controller
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public SuppliersController(ISupplierService supplierService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _supplierService = supplierService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _supplierService.GetPagedAsync(request);

        ViewData["Search"] = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var supplier = await _supplierService.GetByIdWithDueAsync(id);
        if (supplier is null)
        {
            return NotFound();
        }

        ViewBag.PurchaseHistory = await _supplierService.GetPurchaseHistoryAsync(id);
        return View(supplier);
    }

    [HttpGet]
    [PermissionAuthorize("SUPPLIERS", PermissionAction.Create)]
    public IActionResult Create() => View(new Supplier());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUPPLIERS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Supplier model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _supplierService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create supplier.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Suppliers", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Supplier created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("SUPPLIERS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier is null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUPPLIERS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Supplier model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _supplierService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update supplier.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Suppliers", model.SupplierId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("SUPPLIERS", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _supplierService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Suppliers", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Supplier deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
