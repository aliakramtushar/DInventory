using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Category?> GetByIdAsync(int categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Category>("SELECT * FROM dbo.Categories WHERE CategoryId = @categoryId", new { categoryId });
    }

    public async Task<IEnumerable<Category>> GetAllAsync(string? search = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Categories
            WHERE (@search IS NULL OR CategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
            ORDER BY CategoryName";
        return await connection.QueryAsync<Category>(sql, new { search, pattern = $"%{search}%", onlyActive });
    }

    public async Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR CategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Categories {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Categories
            {whereClause}
            ORDER BY CategoryName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Category>(pagedSql, parameters)).ToList();

        return new PagedResult<Category>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Category category)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Categories (CategoryName, Description, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.CategoryId
            VALUES (@CategoryName, @Description, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, category);
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Categories
            SET CategoryName = @CategoryName, Description = @Description, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE CategoryId = @CategoryId";
        var rows = await connection.ExecuteAsync(sql, category);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Categories WHERE CategoryId = @categoryId", new { categoryId });
        return rows > 0;
    }

    public async Task<bool> HasProductsAsync(int categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Products WHERE CategoryId = @categoryId", new { categoryId });
        return count > 0;
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Categories WHERE CategoryName = @name AND (@excludeId IS NULL OR CategoryId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { name, excludeId });
        return count > 0;
    }
}
