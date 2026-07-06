using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class BusinessUnitRepository : IBusinessUnitRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BusinessUnitRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT bu.BusinessUnitId, bu.CompanyId, bu.BusinessUnitName, bu.IsActive,
               bu.CreatedAt, bu.UpdatedAt, bu.CreatedBy, bu.UpdatedBy,
               c.CompanyName
        FROM dbo.BusinessUnits bu
        INNER JOIN dbo.Companies c ON c.CompanyId = bu.CompanyId";

    public async Task<BusinessUnit?> GetByIdAsync(int businessUnitId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BusinessUnit>($"{SelectBase} WHERE bu.BusinessUnitId = @businessUnitId", new { businessUnitId });
    }

    /// <summary>companyId = 0 (superuser) returns business units across every company.</summary>
    public async Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $@"{SelectBase}
            WHERE (@companyId = 0 OR bu.CompanyId = @companyId)
              AND (@search IS NULL OR bu.BusinessUnitName LIKE @pattern)
              AND (@onlyActive = 0 OR bu.IsActive = 1)
            ORDER BY bu.BusinessUnitName";
        return await connection.QueryAsync<BusinessUnit>(sql, new { companyId, search, pattern = $"%{search}%", onlyActive });
    }

    public async Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR bu.CompanyId = @companyId)
              AND (@search IS NULL OR bu.BusinessUnitName LIKE @pattern)
              AND (@onlyActive = 0 OR bu.IsActive = 1)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.BusinessUnits bu
            INNER JOIN dbo.Companies c ON c.CompanyId = bu.CompanyId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY bu.BusinessUnitName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<BusinessUnit>(pagedSql, parameters)).ToList();

        return new PagedResult<BusinessUnit>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(BusinessUnit businessUnit)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.BusinessUnits (CompanyId, BusinessUnitName, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.BusinessUnitId
            VALUES (@CompanyId, @BusinessUnitName, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, businessUnit);
    }

    public async Task<bool> UpdateAsync(BusinessUnit businessUnit)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.BusinessUnits
            SET BusinessUnitName = @BusinessUnitName, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE BusinessUnitId = @BusinessUnitId";
        var rows = await connection.ExecuteAsync(sql, businessUnit);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int businessUnitId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.BusinessUnits WHERE BusinessUnitId = @businessUnitId", new { businessUnitId });
        return rows > 0;
    }

    public async Task<bool> HasDependentDataAsync(int businessUnitId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE BusinessUnitId = @businessUnitId";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { businessUnitId });
        return count > 0;
    }

    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) FROM dbo.BusinessUnits
            WHERE CompanyId = @companyId AND BusinessUnitName = @name AND (@excludeId IS NULL OR BusinessUnitId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }
}
