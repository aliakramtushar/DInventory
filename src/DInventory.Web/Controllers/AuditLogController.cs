using DInventory.Application.Audit;
using DInventory.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class AuditLogController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(string? search, string? action, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 25, Search = search };
        var result = await _auditLogService.GetPagedAsync(request, action, fromDate, toDate?.AddDays(1));

        ViewData["Search"] = search;
        ViewData["Action"] = action;
        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");

        return View(result);
    }
}
