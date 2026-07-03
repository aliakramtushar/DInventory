using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Audit;

public interface IAuditLogService
{
    Task LogAsync(int? userId, string? username, string action, string? tableName = null, string? recordId = null,
        string? oldValues = null, string? newValues = null, string? ipAddress = null);
    Task<PagedResult<AuditLog>> GetPagedAsync(PagedRequest request, string? action = null, DateTime? fromDate = null, DateTime? toDate = null);
}
