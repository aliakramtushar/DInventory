using DInventory.Application.Audit;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Content;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("CONTENT")]
public class ContentController : Controller
{
    private readonly IContentPageService _contentPageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ContentController(
        IContentPageService contentPageService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IAuditLogService auditLogService,
        IWebHostEnvironment hostEnvironment)
    {
        _contentPageService = contentPageService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _auditLogService = auditLogService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _contentPageService.GetPagedAsync(request, effectiveCompanyId);

        ViewData["Search"] = search;
        return View(result);
    }

    [HttpGet]
    [PermissionAuthorize("CONTENT", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        ViewBag.CanCreate = effectiveCompanyId > 0;

        return View(new ContentPage());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CONTENT", PermissionAction.Create)]
    public async Task<IActionResult> Create(ContentPage model, IFormFile? imageFile)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        if (effectiveCompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Please select a company from the top navigation bar first.");
        }
        else
        {
            model.CompanyId = effectiveCompanyId;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CanCreate = effectiveCompanyId > 0;
            return View(model);
        }

        if (imageFile is { Length: > 0 })
        {
            model.ImagePath = await SaveContentImageAsync(imageFile);
        }

        var result = await _contentPageService.CreateAsync(model, currentUser.UserId);

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "ContentPages", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Page created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("CONTENT", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _contentPageService.GetByIdAsync(id);
        if (page is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && page.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CONTENT", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(ContentPage model, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (imageFile is { Length: > 0 })
        {
            model.ImagePath = await SaveContentImageAsync(imageFile);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _contentPageService.UpdateAsync(model, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update page.");
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "ContentPages", model.ContentPageId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Page updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("CONTENT", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _contentPageService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "ContentPages", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Page deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveContentImageAsync(IFormFile file)
    {
        var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "content");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/content/{fileName}";
    }
}
