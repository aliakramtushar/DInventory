using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ContentPageRepository : IContentPageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ContentPageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ContentPage?> GetByIdAsync(int contentPageId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ContentPage>("SELECT * FROM dbo.ContentPages WHERE ContentPageId = @contentPageId", new { contentPageId });
    }

    public async Task<ContentPage?> GetBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ContentPage>("SELECT * FROM dbo.ContentPages WHERE Slug = @slug", new { slug });
    }

    public async Task<IEnumerable<ContentPage>> GetAllAsync(bool onlyPublished = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.ContentPages
            WHERE (@onlyPublished = 0 OR IsPublished = 1)
            ORDER BY DisplayOrder, Title";
        return await connection.QueryAsync<ContentPage>(sql, new { onlyPublished });
    }

    public async Task<PagedResult<ContentPage>> GetPagedAsync(PagedRequest request, int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR Title LIKE @pattern OR Slug LIKE @pattern)";

        var countSql = $"SELECT COUNT(1) FROM dbo.ContentPages {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.ContentPages
            {whereClause}
            ORDER BY DisplayOrder, Title
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
        var items = (await connection.QueryAsync<ContentPage>(pagedSql, parameters)).ToList();

        return new PagedResult<ContentPage>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(ContentPage page)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.ContentPages (Title, Slug, Body, ImagePath, IsPublished, DisplayOrder, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ContentPageId
            VALUES (@Title, @Slug, @Body, @ImagePath, @IsPublished, @DisplayOrder, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, page);
    }

    public async Task<bool> UpdateAsync(ContentPage page)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.ContentPages
            SET Title = @Title, Slug = @Slug, Body = @Body, ImagePath = @ImagePath, IsPublished = @IsPublished,
                DisplayOrder = @DisplayOrder, UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE ContentPageId = @ContentPageId";
        var rows = await connection.ExecuteAsync(sql, page);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int contentPageId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.ContentPages WHERE ContentPageId = @contentPageId", new { contentPageId });
        return rows > 0;
    }

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.ContentPages WHERE Slug = @slug AND (@excludeId IS NULL OR ContentPageId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { slug, excludeId });
        return count > 0;
    }
}
