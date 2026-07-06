using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ColorRepository : IColorRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ColorRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Color?> GetByIdAsync(int colorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Color>("SELECT * FROM dbo.Colors WHERE ColorId = @colorId", new { colorId });
    }

    public async Task<IEnumerable<Color>> GetAllAsync(int companyId, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Colors
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@onlyActive = 0 OR IsActive = 1)
            ORDER BY DisplayOrder, ColorName";
        return await connection.QueryAsync<Color>(sql, new { companyId, onlyActive });
    }

    public async Task<PagedResult<Color>> GetPagedAsync(PagedRequest request, int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = "WHERE (@companyId = 0 OR CompanyId = @companyId) AND (@search IS NULL OR ColorName LIKE @pattern)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Colors {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Colors
            {whereClause}
            ORDER BY DisplayOrder, ColorName, ColorId
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
        var items = (await connection.QueryAsync<Color>(pagedSql, parameters)).ToList();

        return new PagedResult<Color>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Color color)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Colors (ColorName, HexCode, DisplayOrder, CompanyId, BusinessUnitId, IsActive, CreatedAt)
            OUTPUT INSERTED.ColorId
            VALUES (@ColorName, @HexCode, @DisplayOrder, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt)";
        return await connection.ExecuteScalarAsync<int>(sql, color);
    }

    public async Task<bool> UpdateAsync(Color color)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Colors
            SET ColorName = @ColorName, HexCode = @HexCode, DisplayOrder = @DisplayOrder, IsActive = @IsActive
            WHERE ColorId = @ColorId";
        var rows = await connection.ExecuteAsync(sql, color);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int colorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Colors WHERE ColorId = @colorId", new { colorId });
        return rows > 0;
    }

    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Colors WHERE CompanyId = @companyId AND ColorName = @name AND (@excludeId IS NULL OR ColorId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }

    public async Task<bool> HasVariantsAsync(int colorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.ProductVariants WHERE ColorId = @colorId", new { colorId });
        return count > 0;
    }
}
