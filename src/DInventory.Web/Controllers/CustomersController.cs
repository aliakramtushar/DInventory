using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Customers;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("CUSTOMERS")]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
    private readonly IAuditLogService _auditLogService;

    public CustomersController(
        ICustomerService customerService,
        ILoyaltyService loyaltyService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
        IAuditLogService auditLogService)
    {
        _customerService = customerService;
        _loyaltyService = loyaltyService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _customerService.GetPagedAsync(request, effectiveCompanyId);

        ViewData["Search"] = search;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customerService.GetByIdWithStatsAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && customer.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        ViewBag.SalesHistory = await _customerService.GetSalesHistoryAsync(id);
        ViewBag.LoyaltyHistory = await _loyaltyService.GetHistoryAsync(id);
        return View(customer);
    }

    [HttpGet]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        return View(new Customer { CompanyId = effectiveCompanyId, BusinessUnitId = currentUser.BusinessUnitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Customer model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        model.CompanyId = effectiveCompanyId;
        model.BusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        if (model.CompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a customer.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _customerService.CreateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create customer.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Customers", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Customer created.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customerService.GetByIdAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && customer.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Customer model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _customerService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update customer.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Customers", model.CustomerId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Customer updated.";
        return RedirectToAction(nameof(Details), new { id = model.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customerService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Customers", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Customer deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ---- Loyalty: settings (shop-wide) + manual per-customer point adjustment ------------------

    [HttpGet]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Edit)]
    public async Task<IActionResult> LoyaltySettings()
    {
        var settings = await _loyaltyService.GetSettingsAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Edit)]
    public async Task<IActionResult> LoyaltySettings(DInventory.Domain.Entities.LoyaltySettings model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _loyaltyService.UpdateSettingsAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update loyalty settings.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "LoyaltySettings", model.LoyaltySettingsId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Loyalty settings updated.";
        return RedirectToAction(nameof(LoyaltySettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CUSTOMERS", PermissionAction.Edit)]
    public async Task<IActionResult> AdjustPoints(int customerId, int points, string? remarks)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _loyaltyService.AdjustAsync(customerId, points, remarks, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "ADJUST", "LoyaltyTransactions", customerId.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Loyalty points adjusted.";
        }

        return RedirectToAction(nameof(Details), new { id = customerId });
    }

}
