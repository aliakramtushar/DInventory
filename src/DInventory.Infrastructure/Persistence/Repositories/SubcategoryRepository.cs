using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class SubcategoryRepository : ISubcategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SubcategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT s.SubcategoryId, s.CategoryId, s.SubcategoryName, s.Description, s.IsActive,
               s.CreatedAt, s.UpdatedAt, s.CreatedBy, s.UpdatedBy, c.CategoryName
        FROM dbo.Subcategories s
        INNER JOIN dbo.Categories c ON c.CategoryId = s.CategoryId";

    public async Task<Subcategory?> GetByIdAsync(int subcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Subcategory>($"{SelectBase} WHERE s.SubcategoryId = @subcategoryId", new { subcategoryId });
    }

    public async Task<IEnumerable<Subcategory>> GetAllAsync(int? categoryId = null, string? search = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $@"{SelectBase}
            WHERE (@categoryId IS NULL OR s.CategoryId = @categoryId)
              AND (@search IS NULL OR s.SubcategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR s.IsActive = 1)
            ORDER BY s.SubcategoryName";
        return await connection.QueryAsync<Subcategory>(sql, new { categoryId, search, pattern = $"%{search}%", onlyActive });
    }

    public async Task<PagedResult<Subcategory>> GetPagedAsync(PagedRequest request, int? categoryId = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@categoryId IS NULL OR s.CategoryId = @categoryId)
              AND (@search IS NULL OR s.SubcategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR s.IsActive = 1)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.Subcategories s
            INNER JOIN dbo.Categories c ON c.CategoryId = s.CategoryId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY s.SubcategoryName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            categoryId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Subcategory>(pagedSql, parameters)).ToList();

        return new PagedResult<Subcategory>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Subcategory subcategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Subcategories (CategoryId, SubcategoryName, Description, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.SubcategoryId
            VALUES (@CategoryId, @SubcategoryName, @Description, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, subcategory);
    }

    public async Task<bool> UpdateAsync(Subcategory subcategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Subcategories
            SET CategoryId = @CategoryId, SubcategoryName = @SubcategoryName, Description = @Description,
                IsActive = @IsActive, UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE SubcategoryId = @SubcategoryId";
        var rows = await connection.ExecuteAsync(sql, subcategory);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int subcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Subcategories WHERE SubcategoryId = @subcategoryId", new { subcategoryId });
        return rows > 0;
    }

    public async Task<bool> HasProductsAsync(int subcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Products WHERE SubcategoryId = @subcategoryId", new { subcategoryId });
        return count > 0;
    }
}
