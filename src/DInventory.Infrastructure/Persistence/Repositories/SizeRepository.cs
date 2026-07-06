using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class SizeRepository : ISizeRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SizeRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Size?> GetByIdAsync(int sizeId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Size>("SELECT * FROM dbo.Sizes WHERE SizeId = @sizeId", new { sizeId });
    }

    public async Task<IEnumerable<Size>> GetAllAsync(int companyId, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Sizes
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@onlyActive = 0 OR IsActive = 1)
            ORDER BY DisplayOrder, SizeName";
        return await connection.QueryAsync<Size>(sql, new { companyId, onlyActive });
    }

    public async Task<PagedResult<Size>> GetPagedAsync(PagedRequest request, int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = "WHERE (@companyId = 0 OR CompanyId = @companyId) AND (@search IS NULL OR SizeName LIKE @pattern)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Sizes {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Sizes
            {whereClause}
            ORDER BY DisplayOrder, SizeName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Size>(pagedSql, parameters)).ToList();

        return new PagedResult<Size>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Size size)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Sizes (SizeName, DisplayOrder, CompanyId, BusinessUnitId, IsActive, CreatedAt)
            OUTPUT INSERTED.SizeId
            VALUES (@SizeName, @DisplayOrder, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt)";
        return await connection.ExecuteScalarAsync<int>(sql, size);
    }

    public async Task<bool> UpdateAsync(Size size)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Sizes
            SET SizeName = @SizeName, DisplayOrder = @DisplayOrder, IsActive = @IsActive
            WHERE SizeId = @SizeId";
        var rows = await connection.ExecuteAsync(sql, size);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int sizeId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Sizes WHERE SizeId = @sizeId", new { sizeId });
        return rows > 0;
    }

    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Sizes WHERE CompanyId = @companyId AND SizeName = @name AND (@excludeId IS NULL OR SizeId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }

    public async Task<bool> HasVariantsAsync(int sizeId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.ProductVariants WHERE SizeId = @sizeId", new { sizeId });
        return count > 0;
    }
}
