using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Audit;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task LogAsync(int? userId, string? username, string action, string? tableName = null, string? recordId = null,
        string? oldValues = null, string? newValues = null, string? ipAddress = null)
    {
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            OldValues = oldValues,
            NewValues = newValues,
            IPAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task<PagedResult<AuditLog>> GetPagedAsync(PagedRequest request, string? action = null, DateTime? fromDate = null, DateTime? toDate = null)
        => _auditLogRepository.GetPagedAsync(request, action, fromDate, toDate);
}
