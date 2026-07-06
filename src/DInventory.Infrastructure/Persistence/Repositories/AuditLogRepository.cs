using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(AuditLog log)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.AuditLogs (UserId, Username, Action, TableName, RecordId, OldValues, NewValues, IPAddress, CreatedAt)
            OUTPUT INSERTED.AuditLogId
            VALUES (@UserId, @Username, @Action, @TableName, @RecordId, @OldValues, @NewValues, @IPAddress, @CreatedAt)";
        return (int)await connection.ExecuteScalarAsync<long>(sql, log);
    }

    public async Task<PagedResult<AuditLog>> GetPagedAsync(PagedRequest request, string? action = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string whereClause = @"
            WHERE (@search IS NULL OR Username LIKE @pattern OR TableName LIKE @pattern OR RecordId LIKE @pattern)
              AND (@action IS NULL OR Action = @action)
              AND (@fromDate IS NULL OR CreatedAt >= @fromDate)
              AND (@toDate IS NULL OR CreatedAt < @toDate)";

        var countSql = $"SELECT COUNT(1) FROM dbo.AuditLogs {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.AuditLogs
            {whereClause}
            ORDER BY CreatedAt DESC, AuditLogId DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            action,
            fromDate,
            toDate,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<AuditLog>(pagedSql, parameters)).ToList();

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
