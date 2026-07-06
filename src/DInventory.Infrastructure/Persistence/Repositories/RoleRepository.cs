using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Role?> GetByIdAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Role>("SELECT * FROM dbo.Roles WHERE RoleId = @roleId", new { roleId });
    }

    public async Task<Role?> GetByNameAsync(string roleName)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Role>("SELECT * FROM dbo.Roles WHERE RoleName = @roleName", new { roleName });
    }

    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Role>("SELECT * FROM dbo.Roles ORDER BY RoleId");
    }

    public async Task<PagedResult<Role>> GetPagedAsync(PagedRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = "WHERE (@search IS NULL OR RoleName LIKE @pattern)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Roles {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Roles
            {whereClause}
            ORDER BY RoleName, RoleId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Role>(pagedSql, parameters)).ToList();

        return new PagedResult<Role>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Role role)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Roles (RoleName, Description, IsSystemRole, IsActive, CreatedAt)
            OUTPUT INSERTED.RoleId
            VALUES (@RoleName, @Description, @IsSystemRole, @IsActive, @CreatedAt)";
        return await connection.ExecuteScalarAsync<int>(sql, role);
    }

    public async Task<bool> UpdateAsync(Role role)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Roles
            SET RoleName = @RoleName, Description = @Description, IsActive = @IsActive, UpdatedAt = @UpdatedAt
            WHERE RoleId = @RoleId";
        var rows = await connection.ExecuteAsync(sql, role);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Roles WHERE RoleId = @roleId", new { roleId });
        return rows > 0;
    }

    public async Task<int> GetUserCountForRoleAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Users WHERE RoleId = @roleId", new { roleId });
    }
}
