using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IAuditLogRepository
{
    Task<int> CreateAsync(AuditLog log);
    Task<PagedResult<AuditLog>> GetPagedAsync(PagedRequest request, string? action = null, DateTime? fromDate = null, DateTime? toDate = null);
}
