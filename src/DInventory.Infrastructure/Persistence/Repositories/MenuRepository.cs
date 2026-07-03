using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class MenuRepository : IMenuRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MenuRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Menu?> GetByIdAsync(int menuId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Menu>("SELECT * FROM dbo.Menus WHERE MenuId = @menuId", new { menuId });
    }

    public async Task<Menu?> GetByKeyAsync(string menuKey)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Menu>("SELECT * FROM dbo.Menus WHERE MenuKey = @menuKey", new { menuKey });
    }

    public async Task<IEnumerable<Menu>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Menu>("SELECT * FROM dbo.Menus ORDER BY DisplayOrder, MenuName");
    }

    public async Task<PagedResult<Menu>> GetPagedAsync(PagedRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = "WHERE (@search IS NULL OR MenuName LIKE @pattern OR MenuKey LIKE @pattern)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Menus {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Menus
            {whereClause}
            ORDER BY DisplayOrder, MenuName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Menu>(pagedSql, parameters)).ToList();

        return new PagedResult<Menu>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Menu menu)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
            OUTPUT INSERTED.MenuId
            VALUES (@MenuKey, @MenuName, @Icon, @Url, @ParentId, @DisplayOrder, @IsActive)";
        return await connection.ExecuteScalarAsync<int>(sql, menu);
    }

    public async Task<bool> UpdateAsync(Menu menu)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Menus
            SET MenuKey = @MenuKey, MenuName = @MenuName, Icon = @Icon, Url = @Url,
                ParentId = @ParentId, DisplayOrder = @DisplayOrder, IsActive = @IsActive
            WHERE MenuId = @MenuId";
        var rows = await connection.ExecuteAsync(sql, menu);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int menuId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Menus WHERE MenuId = @menuId", new { menuId });
        return rows > 0;
    }
}
