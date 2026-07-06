using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT u.UserId, u.Username, u.Email, u.PasswordHash, u.FullName, u.RoleId, u.IsActive,
               u.ProfileImage, u.LastLoginAt, u.CreatedAt, u.UpdatedAt, u.CreatedBy, u.UpdatedBy,
               u.CompanyId, u.BusinessUnitId,
               r.RoleName, c.CompanyName, bu.BusinessUnitName
        FROM dbo.Users u
        INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
        LEFT JOIN dbo.Companies c ON c.CompanyId = u.CompanyId
        LEFT JOIN dbo.BusinessUnits bu ON bu.BusinessUnitId = u.BusinessUnitId";

    public async Task<User?> GetByIdAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>($"{SelectBase} WHERE u.UserId = @userId", new { userId });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>($"{SelectBase} WHERE u.Username = @username", new { username });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>($"{SelectBase} WHERE u.Email = @email", new { email });
    }

    public async Task<IEnumerable<User>> GetAllAsync(string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"{SelectBase} WHERE (@search IS NULL OR u.Username LIKE @pattern OR u.FullName LIKE @pattern OR u.Email LIKE @pattern) ORDER BY u.FullName";
        return await connection.QueryAsync<User>(sql, new { search, pattern = $"%{search}%" });
    }

    public async Task<PagedResult<User>> GetPagedAsync(PagedRequest request, int? companyId = null, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR u.Username LIKE @pattern OR u.FullName LIKE @pattern OR u.Email LIKE @pattern)
              AND (@companyId IS NULL OR u.CompanyId = @companyId)
              AND (@businessUnitId IS NULL OR u.BusinessUnitId = @businessUnitId)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.Users u
            INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY u.FullName, u.UserId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            companyId,
            businessUnitId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<User>(pagedSql, parameters)).ToList();

        return new PagedResult<User>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Users (Username, Email, PasswordHash, FullName, RoleId, IsActive, ProfileImage, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.UserId
            VALUES (@Username, @Email, @PasswordHash, @FullName, @RoleId, @IsActive, @ProfileImage, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task<bool> UpdateAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Users
            SET Username = @Username, Email = @Email, FullName = @FullName, RoleId = @RoleId,
                ProfileImage = @ProfileImage, CompanyId = @CompanyId, BusinessUnitId = @BusinessUnitId,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE UserId = @UserId";
        var rows = await connection.ExecuteAsync(sql, user);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Users WHERE UserId = @userId", new { userId });
        return rows > 0;
    }

    public async Task<bool> SetActiveAsync(int userId, bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("UPDATE dbo.Users SET IsActive = @isActive, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @userId", new { userId, isActive });
        return rows > 0;
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("UPDATE dbo.Users SET PasswordHash = @passwordHash, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @userId", new { userId, passwordHash });
        return rows > 0;
    }

    public async Task<bool> UpdateLastLoginAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("UPDATE dbo.Users SET LastLoginAt = SYSUTCDATETIME() WHERE UserId = @userId", new { userId });
        return rows > 0;
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE Username = @username AND (@excludeUserId IS NULL OR UserId <> @excludeUserId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { username, excludeUserId });
        return count > 0;
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE Email = @email AND (@excludeUserId IS NULL OR UserId <> @excludeUserId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { email, excludeUserId });
        return count > 0;
    }
}
