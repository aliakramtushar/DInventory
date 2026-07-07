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

    /// <summary>companyId = 0 (superuser) bypasses the filter and returns categories across every company.</summary>
    public async Task<IEnumerable<Category>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Categories
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR CategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)
            ORDER BY CategoryName";
        return await connection.QueryAsync<Category>(sql, new { companyId, search, pattern = $"%{search}%", onlyActive, businessUnitId });
    }

    public async Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR CategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Categories {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Categories
            {whereClause}
            ORDER BY CategoryName, CategoryId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            businessUnitId,
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
            INSERT INTO dbo.Categories (CategoryName, Description, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.CategoryId
            VALUES (@CategoryName, @Description, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
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

    /// <summary>Category names only need to be unique within a company, not globally, since two
    /// unrelated tenants may both want a "Men's Wear" category.</summary>
    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Categories WHERE CompanyId = @companyId AND CategoryName = @name AND (@excludeId IS NULL OR CategoryId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }
}
